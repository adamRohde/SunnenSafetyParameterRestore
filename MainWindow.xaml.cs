
using EAL.Enums;
using EAL.Interfaces;
using EAL.Interfaces.Motion;
using EAL.Interfaces.Parameter;
using EAL.Interfaces.System;
using EAL.Parameter.ParameterReadWrite;
using EAL.Structures;
using EAL.Exceptions;
using EAL.EALGeneral.System;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EAL.EALConnection;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;

namespace _6112020_SunnenSafetyParameterEALTest
{

    public partial class MainWindow : Window
    {
        private IEALConnection _ealConnection = null;
        private const string ConfigFile = "ConfigFile.xml";
        private BackgroundWorker _backgroundWorker;
        private SynchronizationContext _context;
        private string _filePath;
        private DispatcherTimer m_DispatcherTimer = null;
        private DiagnosisTraceLog[] diagTraceLogArray = null;
        private ObservableCollection<LogMessage> LogMessages { get; set; }  //Must be set public so binding to the UI works (Listview)
        private static Color ColorHighlight = Colors.Yellow;

        public MainWindow()
        {
            InitializeComponent();
            
            _ealConnection = new EALConnection();
            _backgroundWorker = new BackgroundWorker();
            _backgroundWorker.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
            _backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_backgroundWorker_RunWorkerCompleted);
            _context = SynchronizationContext.Current;
            var _executeCommand = new ExecuteCommand(_ealConnection);
            LogMessages = new ObservableCollection<LogMessage>();  // Needs to be initialized here or else you get "Object reference not set to an instance of a object"
            ColorHighlight = Colors.Yellow;
        }

        int i = 0;
        private void StartTimer()
        {
            if (m_DispatcherTimer is null)
            {
                // setup our timer
                try
                {
                    m_DispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Send, DispatcherTimer_Tick, Dispatcher.CurrentDispatcher);
                    m_DispatcherTimer.Start();
                }
                catch (Exception ex)
                {
                    Debug.Assert(false);
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void StopTimer()
        {
            if (m_DispatcherTimer != null)
            {
                m_DispatcherTimer.Stop();
                m_DispatcherTimer = null;
            }
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {         
            Console.WriteLine("Hello" + " " + i++);
        }

        // #################################################################################################################################################

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                ArgmentForLoadParameters arg = new ArgmentForLoadParameters();
                arg.slaveIndex = 0; arg.FileOrderIndex = 0;
                //// Registring events
                _ealConnection.Parameter.Axes[arg.slaveIndex].StatusUpdate += new OnParameterReadWriteHandler(Parameter_StatusUpdate);
                _ealConnection.Parameter.Axes[arg.slaveIndex].MessageUpdate += new OnMessageHandler(Parameter_MessageUpdate);
                // Loads parameter from file.
                _ealConnection.Parameter.Axes[arg.slaveIndex].LoadParameters(_filePath, arg.FileOrderIndex);

                _context.Send(delegate (object state)
                {
                    //makes it so progress par doesn't hang right around 99 percent.  Without this the progress bar never completely fills in.  Doesn't affect how it performs.
                    progressBarLoadingParams.Value = 100;
                }, null);
            }
            catch (Exception ex)
            {
                _context.Send(delegate (object state)
                {
                    MessageBox.Show(ex.Message);
                },
                null);
            }
        }

        private void Parameter_StatusUpdate(object sender, TransferDataEventArgs e)
        {
            if (e.BytesTransferred > e.BytesTotal) {/*Debugger.Break(); */}             
            else
            {
                _context.Send(
                    delegate (object state)
                    {
                        progressBarLoadingParams.Value = (int)((e.BytesTransferred * 100) / e.BytesTotal);

                    },
                null
                );
            }
        }

        private void Parameter_MessageUpdate(object sender, EventArgs e)
        {
            _context.Send(
             delegate (object state)
             {
                 // rTxtStatusUpdate.AppendText( "test " + "\n" + e.Message);
                 Console.WriteLine("Message Update");

             },
         null
         );
        }

        private void Parameter_ErrorOnUpdate(object sender, EventArgs e)
        {
            _context.Send(
          delegate (object state)
          {
              // rTxtStatusUpdate.AppendText("fasdfadsfasdf " + "\n" + e.Parameter + ":" + e.ErrorCode);
          },
          null
          );
        }

        private void _backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
           // MessageBox.Show("Loading Parameter File Successful!");
            progressBarLoadingParams.Value = 0;
            lbStep.Content = "Step 3";
            lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
        }

        // #################################################################################################################################################

        class ArgmentForLoadParameters
        {
            public int slaveIndex { get; set; } = 0;
            public int FileOrderIndex { get; set; } = 0;
        }

        public static string PasswordLevel(int input)
        {
            if (0 == input)
            {
                return "Not active, C0720 allowed";
            }
            if (1==input)
            {
                return "Active, Unlocked, C0720 allowed";
            }
            if (2==input)
            {
                return "Active, Locked, C0720 NOT allowed";
            }
            if (3==input)
            {
                return "Active, Locked, C0720 allowed";
            }

            return " ";
        }

        public static string OperationMode(int input)
        {
            if (0==input)
            {
                return "OM Mode Active";
            }
            if (1==input)
            {
                return "PM Mode Active";
            }

            return " ";
        }

        public void UpdateLogMessagesAll(DiagnosisTraceLog[] tracelog)
        {
            List<ListViewItem> items = new List<ListViewItem>();

            DriveLogger.DiagMessages diagMessages = new DriveLogger.DiagMessages();

            //First time getting the log this statement should run
            if (diagTraceLogArray ==null)
            {
                diagTraceLogArray = tracelog;
                 
                foreach (DiagnosisTraceLog log in tracelog)
                {
                    if ("00000000" != log.diagnosticNumber.ToString("X8"))
                    {
                        diagMessages.DiagNumber = log.diagnosticNumber.ToString("X8");
                        diagMessages.Lookup();

                        ListViewItem OneItem = new ListViewItem();
                        OneItem.Content = new LogMessage()
                        {
                            LogEvent = log.diagnosticNumber.ToString("X8").Remove(0, 3),
                            LogEventDescription =  diagMessages.DiagText,
                            LogTime = log.dateTime
                        };
                        if (true == log.diagnosticNumber.ToString("X8").Remove(0, 3).Contains("A03"))
                        {
                            OneItem.Background = Brushes.Yellow;
                        }
                        else
                        {
                            OneItem.Background = Brushes.WhiteSmoke;
                        }
                        items.Add(OneItem);
                        listView_Logger.ItemsSource = items;
                    }
                }

                Console.WriteLine("DiagTraceLogArray is null son!");
            }

            //When the log is updated this statement will run
            if (diagTraceLogArray != tracelog)
                {
                for (int i = 0; i < tracelog.Length-1; i++)
                {
                    if ("00000000" != tracelog[i].diagnosticNumber.ToString("X8"))
                    {
                        if (diagTraceLogArray[i].detailedDiagnostic != tracelog[i].diagnosticNumber)
                        {
                            diagMessages.DiagNumber = tracelog[i].diagnosticNumber.ToString("X8");
                            diagMessages.Lookup();

                            ListViewItem OneItem = new ListViewItem();
                           
                            OneItem.Content = new LogMessage()
                            {
                                LogEvent = tracelog[i].diagnosticNumber.ToString("X8").Remove(0, 3),
                                LogEventDescription = diagMessages.DiagText,
                                LogTime = tracelog[i].dateTime
                            };

                            if (true == tracelog[i].diagnosticNumber.ToString("X8").Remove(0, 3).Contains("A03"))
                            {
                                OneItem.Background = Brushes.Yellow;
                            }
                            else
                            {
                                OneItem.Background = Brushes.WhiteSmoke;
                            }

                            items.Add(OneItem);
                            listView_Logger.ItemsSource = items;
                        }
                    }
                }
                Console.WriteLine("DiagTraceLogArray is NOT null son!");
            }

            listView_Logger.Items.Refresh();
            diagTraceLogArray = tracelog;
        }

        //##################################################################################################################################################

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _ealConnection.Connect(tbIPAddress.Text);

                if (_ealConnection.IsConnected == true)
                {
                    lbStep.Content = "Step 1";
                    tbActiveAxisIdentifier.Text = _ealConnection.Parameter.ReadDataAsString("P-0-3235.0.1");
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                    lbOperatingMode.Content = OperationMode(_ealConnection.Parameter.ReadDataInt("S-0-0424.0.0"));
                    Console.WriteLine($"\n Drive has been connected \n");

                    lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                    try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                    catch (EALException ex) { MessageBox.Show(ex.Message); }
                }
            }
            catch (EALException ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Diconnect_Click(object sender, RoutedEventArgs e)
        {
            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Disconnect();
                    _ealConnection.WaitUntilQueueComplete();
                    Console.WriteLine("Drive has been disconnected");
               
                    lbOperatingMode.Content = " ";
                    lbSafetyPasswordLevel.Content = "Disconnected";

                    btnDiconnect.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void PM_Click(object sender, RoutedEventArgs e)                         //Button Parameter Mode
        {
            bool result;

            if (_ealConnection.IsConnected == true)
            {
                result = ExecuteCommand.RunCommand("S-0-0420.0.0");
                Console.WriteLine("Result =  " + result + "\n" + "\n");

                lbOperatingMode.Content = OperationMode(_ealConnection.Parameter.ReadDataInt("S-0-0424.0.0"));
                lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;


                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void OP_Click(object sender, RoutedEventArgs e)
        {
            bool result;

            if (_ealConnection.IsConnected == true)
            {
                result = ExecuteCommand.RunCommand("S-0-0422.0.0");
                Console.WriteLine("Result =  " + result + "\n" + "\n");
               
                if (result == true)
                {
                    lbStep.Content = "Complete";

                }else
                {
                   // lbStep.Content = "Error";
                }

                lbOperatingMode.Content = OperationMode(_ealConnection.Parameter.ReadDataInt("S-0-0424.0.0"));
                lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;


                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (EALException ex) {

                    Console.WriteLine(ex.Message);
                    
                   // MessageBox.Show(ex.Message);
                }
            }
        }

        private void ClearError_Click(object sender, RoutedEventArgs e)
        {
            bool result;

            if (_ealConnection.IsConnected == true)
            {
                result = ExecuteCommand.RunCommand("S-0-0099.0.0");
                Console.WriteLine("Result =  " + result);

                lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;


                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

            }
        }

        //##################################################################################################################################################

        private void btnLoadDriveDefaults_Click(object sender, RoutedEventArgs e)
        {
            bool result;

            if (_ealConnection.IsConnected == true)
            {

                try
                {
                    //string ident = _ealConnection.Parameter.ReadData("P-0-3235.0.1").ToString(); //Gets current axis identifier
     
                    _ealConnection.Parameter.WriteData(17, "P-0-4090");  //Configuration for loading default procedure. 3 specifies writing safety parameters to default value and also standard parameters
                    Console.WriteLine("Write Configuration for loading defaults params to 17 - P-0-4090");

                    _ealConnection.Parameter.WriteData(_ealConnection.Parameter.ReadData("P-0-3235.0.1").ToString() , "P-0-3230.0.1"); //Must write axis indentifier to password parameter so acknowledge the drive you are defaulting
                    Console.WriteLine(tbActiveAxisIdentifier.Text + " " + "Write Identifier to P-0-3230.0.1 (password parameter) for verification");

                    result = ExecuteCommand.RunCommand("S-0-0262.0.0"); //Load factory default procedure command
                    Console.WriteLine("Result =  " + result);

                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status

                }
                catch (Exception ex)
                {
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                    MessageBox.Show(ex.Message);

                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void ActivateSafety_Click(object sender, RoutedEventArgs e)
        {
            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Parameter.WriteData("INDRASAVE" + " " + tbPassword.Text + " " + tbPassword.Text, "P-0-3230.0.1");
                    tbActiveAxisIdentifier.Text = _ealConnection.Parameter.ReadData("P-0-3235.0.1").ToString(); //Reads the current Axis Identifier
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                }
                catch (Exception ex)
                {
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                    MessageBox.Show(ex.Message);
                }

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

            }
        }

        //##################################################################################################################################################
        // #################################################################### Button  ####################################################################
        //##################################################################################################################################################

        // #########################################  1 #########################################

        private void btnLoadSMODefaults_Click(object sender, RoutedEventArgs e)
        {
            bool result;

            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Parameter.WriteData(tbIdentifierPrimer.Text, "P-0-3230.0.1");

                    result = ExecuteCommand.RunCommand("S-0-0262.0.0"); //Load factory default procedure command
                    Console.WriteLine("Result =  " + result);

                    tbActiveAxisIdentifier.Text = _ealConnection.Parameter.ReadData("P-0-3235.0.1").ToString(); //Reads the current Axis Identifier

                    tbIdentifierPrimer.Text = " ";

                    if (result == true)
                    {
                        lbStep.Content = "Step 2";
                    }
                    else
                    {
                        lbStep.Content = "Error";
                    }

                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                }

                catch (Exception ex)
                {
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                    MessageBox.Show(ex.Message);
                }

                try
                {
                    UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // #########################################  2 #########################################


        private void btnLoadParamFile_Click(object sender, EventArgs e)              
        {
            string filename = tbLoadParametersPath.Text;
            ArgmentForLoadParameters arg = new ArgmentForLoadParameters();
            arg.slaveIndex = 0;
            arg.FileOrderIndex = 0;

            _filePath = tbLoadParametersPath.Text;

            // Starts background thread
            _backgroundWorker.RunWorkerAsync();
        }


        private void tbP4090_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Parameter.WriteData(5, "P-0-4090");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

        }

        // #########################################  3 #########################################

        private void btnActivateParamImage_Click(object sender, RoutedEventArgs e)
        {
            bool result;
          
            if (_ealConnection.IsConnected == true)
            {
                result = ExecuteCommand.RunCommand("P-0-3231.0.3");
                Console.WriteLine("Result =  " + result);

                if (result == true)
                {
                    lbStep.Content = "Step 4";
                }
                else
                {
                    lbStep.Content = "Error";
                }

                lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // #########################################  4 #########################################

        private void ActivateSCM_Click(object sender, RoutedEventArgs e)
        {
          
            if (_ealConnection.IsConnected == true)
            {
                bool result;

                if (_ealConnection.IsConnected == true)
                {
                    result = ExecuteCommand.RunCommand("P-0-3231.0.1");
                    Console.WriteLine("Result =  " + result);

                    if (result == true)
                    {
                        lbStep.Content = "Step 5";
                    }
                    else
                    {
                        lbStep.Content = "Error";
                    }
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }

            }
        }

        // #########################################  5 #########################################

        private void btnEnterAxisIdentifiers_Click(object sender, RoutedEventArgs e)                   
        {
            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Parameter.WriteData(tbAxisIdentifier.Text, "P-0-3235.0.6");
                    _ealConnection.Parameter.WriteData(int.Parse(tbAxisFSOEIdentifier.Text), "P-0-3353.0.0");
                
                    lbStep.Content = "Step 6";
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status

                }
                catch (Exception ex)
                {
                    lbStep.Content = "Error";

                    MessageBox.Show(ex.Message);
                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // #########################################  6 #########################################

        private void btnControlWordBit2_Click(object sender, RoutedEventArgs e)                     
        {
            if (_ealConnection.IsConnected == true)
            {
                try
                {
                    _ealConnection.Parameter.WriteData(4, "P-0-3235.0.4");

                    // tbApplyID6.Text = _ealConnection.Parameter.ReadData("P-0-3235.0.4").ToString(); //P-0-3235.0.3 Write Bit 2
  
                    lbStep.Content = "Step 7";                
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status

                }
                catch (Exception ex)
                {
                    lbStep.Content = "Error";
                    MessageBox.Show(ex.Message);
                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // #########################################  7 #########################################

        private void btnApplyAxisIdentifiers_Click(object sender, RoutedEventArgs e)
        {

            if (_ealConnection.IsConnected == true)
            {
                bool result;

                if (_ealConnection.IsConnected == true)
                {
                    result = ExecuteCommand.RunCommand("P-0-3235.0.3");
                    Console.WriteLine("Result =  " + result);

                    tbActiveAxisIdentifier.Text = _ealConnection.Parameter.ReadData("P-0-3235.0.1").ToString(); //Reads the current Axis Identifier

                    if (result == true)
                    {
                        lbStep.Content = "Step 8";
                    }
                    else
                    {
                        lbStep.Content = "Error";
                    }
                    lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status
                }

                lbStatus.Content = _ealConnection.System.Axes[0].GetDisplayedDiagnosis().text;

                try { UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        // #########################################  8 #########################################

        private void ExitSCM_Click(object sender, RoutedEventArgs e)                       
        {
            bool result;
          
            if (_ealConnection.IsConnected == true)
            {
                result = ExecuteCommand.RunCommand("P-0-3231.0.2");
                Console.WriteLine("Result =  " + result);

                if (result == true)
                {
                    lbStep.Content = "Go to Op";
                }
                else
                {
                    lbStep.Content = "Error";
                }
                lbSafetyPasswordLevel.Content = PasswordLevel(_ealConnection.Parameter.ReadDataInt("P-0-3230.0.0"));  //Looks at safety status

                try{ UpdateLogMessagesAll(_ealConnection.System.Axes[0].GetDiagnosticTraceLogs()); }
                catch (Exception ex){ MessageBox.Show(ex.Message);  }
            }

        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            if (fd.ShowDialog() == true)
            {
                tbLoadParametersPath.Text = fd.FileName;
            }
        }

        private string ReadParams(string parameter)
        {
            string result = _ealConnection.Parameter.ReadDataAsString(parameter);
            return result;
        }

    }
}
