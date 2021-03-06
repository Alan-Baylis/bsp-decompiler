using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using Microsoft.Win32;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Threading;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Shell;

using LibBSP;

namespace Decompiler.GUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private JobQueueManager jobs = new JobQueueManager();

		private string outputFolder = "";

		private MapType openAs = MapType.Undefined;
		private MenuItem _OpenAsChecked = null;

		/// <summary>
		/// Initializes a new <see cref="MainWindow"/> object.
		/// </summary>
		public MainWindow() {
			InitializeComponent();
			jobListView.ItemsSource = jobs;
			taskBarItemInfo1.ProgressState = TaskbarItemProgressState.Normal;
			Job.MessageEvent += Print;
			Job.JobFinishedEvent += JobFinished;
			if (Revision.version == "") {
				this.Title = "BSP Decompiler v5 by 005";
			} else {
				this.Title = "BSP Decompiler v5." + Revision.version + " by 005";
			}
		}

		/// <summary>
		/// Handler for File -&gt; Open BSP menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void FileOpen_Click(object sender, RoutedEventArgs e) {
			ShowOpenDialog(false);
		}

		/// <summary>
		/// Handler for all File -&gt; Open As menu options.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void OpenAs_Click(object sender, RoutedEventArgs e) {
			if (_OpenAsChecked != null) {
				_OpenAsChecked.IsChecked = false;
			} else {
				miOpenAsAuto.IsChecked = false;
			}
			_OpenAsChecked = e.Source as MenuItem;
			if (_OpenAsChecked != null) {
				_OpenAsChecked.IsChecked = true;
				openAs = (MapType)Int32.Parse(_OpenAsChecked.Tag.ToString());
			}
			EnableFileOpenMapIfAble();
		}

		/// <summary>
		/// Handler for File -&gt; Output Format -&gt; Auto menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void OutputAuto_Click(object sender, RoutedEventArgs e) {
			miSaveAsAuto.IsChecked = true;
			miSaveAsVMF.IsChecked = false;
			miSaveAsMOH.IsChecked = false;
			miSaveAsGC.IsChecked = false;
			miSaveAsGTK.IsChecked = false;
			miSaveAsDE.IsChecked = false;
			EnableFileOpenMapIfAble();
		}

		/// <summary>
		/// Enables the File -&gt; Open Map option if current settings allow it.
		/// </summary>
		private void EnableFileOpenMapIfAble() {
			miOpenMap.IsEnabled = !miSaveAsAuto.IsChecked && !miOpenAsAuto.IsChecked;
		}

		/// <summary>
		/// Handler for File -&gt; Open MAP or VMF menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void FileOpenMAP_Click(object sender, RoutedEventArgs e) {
			ShowOpenDialog(true);
		}

		/// <summary>
		/// Shows the Open dialog, for compiled or uncompiled maps.
		/// </summary>
		/// <param name="uncompiled">Is the map we're looking for uncompiled?</param>
		private void ShowOpenDialog(bool uncompiled) {
			OpenFileDialog fileOpener = new OpenFileDialog();
			if (uncompiled) {
				fileOpener.Filter = "Uncompiled Map Files|*.map;*.vmf|MAP Files|*.map|VMF Files|*.vmf|All files|*.*";
			} else {
				fileOpener.Filter = "BSP Files|*.bsp;*.d3dbsp|All Files|*.*";
			}
			fileOpener.Multiselect = true;

			// Process open file dialog box results 
			if (fileOpener.ShowDialog() == true) {
				string[] filesToOpen = fileOpener.FileNames;
				for (int i = 0; i < filesToOpen.Length; ++i) {
					Job.Settings settings = new Job.Settings() {
						replace512WithNull = miSpecialNull.IsChecked,
						noFaceFlags = miIgnoreFaceFlags.IsChecked,
						brushesToWorld = miToWorld.IsChecked,
						noTexCorrection = miNoTextureCorrect.IsChecked,
						noEntCorrection = miNoEntityCorrect.IsChecked,
						outputFolder = outputFolder,
						openAs = openAs,
						fromUncompiled = uncompiled,
						toAuto = miSaveAsAuto.IsChecked,
						toM510 = miSaveAsGC.IsChecked,
						toVMF = miSaveAsVMF.IsChecked,
						toGTK = miSaveAsGTK.IsChecked,
						toDoomEdit = miSaveAsDE.IsChecked,
						toMoH = miSaveAsMOH.IsChecked
					};
					Job theJob = new Job(jobs.Count, filesToOpen[i], settings);
					theJob.PropertyChanged += new PropertyChangedEventHandler(UpdateTaskbar);
					jobs.Add(theJob);
				}
				jobs.StartNextIfAble();
			}
		}

		/// <summary>
		/// Handler for all File -&gt; Output Format menu options except Auto.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void OutputSpecific_Click(object sender, RoutedEventArgs e) {
			if (!miSaveAsAuto.IsChecked && !miSaveAsVMF.IsChecked && !miSaveAsMOH.IsChecked && !miSaveAsGC.IsChecked && !miSaveAsGTK.IsChecked && !miSaveAsDE.IsChecked) {
				miSaveAsAuto.IsChecked = true;
			} else {
				miSaveAsAuto.IsChecked = false;
			}
			EnableFileOpenMapIfAble();
		}

		/// <summary>
		/// Handler for Options -&gt; Set number of threads menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void NumThreads_Click(object sender, RoutedEventArgs e) {
			try {
				int input = Int32.Parse(Microsoft.VisualBasic.Interaction.InputBox("Please enter number of concurrent decompiles allowed.\nCurrent value: " + jobs.numThreads, "Enter new thread amount", jobs.numThreads.ToString(), -1, -1));
				if (input >= 1) {
					jobs.numThreads = input;
				} else {
					Print(this, new MessageEventArgs("Please enter a whole number greater than 0!"));
				}
			} catch {
				Print(this, new MessageEventArgs("Please enter a whole number greater than 0!"));
			}
		}

		/// <summary>
		/// Handler for Options -&gt; Set output folder menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void OutFolder_Click(object sender, RoutedEventArgs e) {
			try {
				System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
					outputFolder = dialog.SelectedPath + "\\";
					Print(this, new MessageEventArgs(outputFolder));
				} else {
					outputFolder = "";
				}
			} catch {
				outputFolder = "";
			}
		}

		/// <summary>
		/// Handler for Debug -&gt; Save log menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void SaveLog_Click(object sender, RoutedEventArgs e) {
			try {
				SaveFileDialog dialog = new SaveFileDialog();
				dialog.Filter = "Text file|*.txt";
				if (dialog.ShowDialog() == true) {
					FileStream stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write);
					BinaryWriter bw = new BinaryWriter(stream);
					stream.Seek(0, SeekOrigin.Begin);
					bw.Write(txtConsole.Text);
					bw.Close();
				}
			} catch {
				Print(this, new MessageEventArgs("Unable to write file! Make sure you have access to the directory."));
			}
		}

		/// <summary>
		/// Handler for Debug -&gt; Clear log menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void ClearLog_Click(object sender, RoutedEventArgs e) {
			txtConsole.Text = "";
		}

		/// <summary>
		/// Handler for File -&gt; Quit menu option.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>RoutedEventArgs</c> for this event.</param>
		private void Quit_Click(object sender, RoutedEventArgs e) {
			Application.Current.Shutdown();
		}

		/// <summary>
		/// Handles an error in a <see cref="Job"/> and tells the <see cref="JobQueueManager"/> to start another.
		/// </summary>
		/// <param name="job"><see cref="Job"/> that has a problem.</param>
		private void Error(Job job) {
			taskBarItemInfo1.ProgressState = TaskbarItemProgressState.Error;
			jobs.RemoveActive(job);
			jobs.StartNextIfAble();
		}

		/// <summary>
		/// Appends a <c>string</c> to the log output.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><see cref="MessageEventArgs"/> containing the <c>string</c> to append and an optional error flag.</param>
		private void Print(object sender, MessageEventArgs e) {
			this.Dispatcher.Invoke(() => {
				txtConsole.AppendText(e.message + "\n");
				if (txtConsole.SelectionLength == 0) {
					txtConsole.ScrollToEnd();
				}
				if (e.error) {
					Error(sender as Job);
				}
			});
		}

		/// <summary>
		/// Handler for when a <see cref="Job"/> finishes, tells the <see cref="JobQueueManager"/> to start another.
		/// </summary>
		/// <param name="sender">Sender of this event, should be the <see cref="Job"/> that was completed.</param>
		/// <param name="e"><c>EventArgs</c> for this event. Can be <c>EventArgs.Empty</c>.</param>
		private void JobFinished(object sender, EventArgs e) {
			this.Dispatcher.Invoke(() => {
				jobs.RemoveActive((Job)sender);
				jobs.StartNextIfAble();
			});
		}

		/// <summary>
		/// Handler to update the taskbar progress indicator.
		/// </summary>
		/// <param name="sender">Sender of this event.</param>
		/// <param name="e"><c>EventArgs</c> for this event.</param>
		private void UpdateTaskbar(object sender, EventArgs e) {
			double cumulativeProgress = 0.0;
			foreach (Job job in jobs) {
				cumulativeProgress += job.progress;
			}
			this.Dispatcher.Invoke(() => {
				taskBarItemInfo1.ProgressValue = (cumulativeProgress / (double)jobs.Count);
			});
		}
	}
}
