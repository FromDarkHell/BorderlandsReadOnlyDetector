#region Using Statements
using System.Windows.Forms;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;
using static BLIO;

#region Generic System Imports

using System;
using System.Collections.Generic;
using System.Windows;
using System.Text;
using System.Linq;
#endregion

#region IO
using System.IO;
using System.IO.Pipes;
#endregion

#region Threading / Workers
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
#endregion

#region Interops
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Threading;
#endregion

#endregion

namespace ReadOnlyDetector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // TODO: Add support for read-only hotkey toggling.

        #region DLL Imports
        // System functions for listening for hotkeys.
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // System functions for querying process info.
        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool QueryFullProcessImageName([In]IntPtr hProcess, [In]int dwFlags, [Out]StringBuilder lpExeName, ref int lpdwSize);


        // Create a delegate for system events with our callback.
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);


        #endregion

        #region Variables
        // Handle variables for the window.
        private HwndSource source;
        private static IntPtr handle;

        // a bool for if the game is BL2 or naw
        private static bool bl2;

        // Hotkey uINT value
        private const uint VK_readOnlyHotkey = 0xBF;

        // The constant used to distinguish our key listening.
        private const int HOTKEY_ID = 69420;

        // A timer
        private static DispatcherTimer dT = new DispatcherTimer();

        // Process Delegate
        private static readonly WinEventDelegate procDelegate = new WinEventDelegate(WinEventProc);

        // This is the general date format used on the save.
        private static BorderlandsDateFormat dateFormatter;

        // This is just the WPF form we have.
        private static MainWindow window;

        // This is our previous date for saving.
        private static long previousMemoryDate = 0;

        // This is our notification area icon
        private readonly NotifyIcon nIcon = new NotifyIcon();

        // This is our checkbox used for read-only
        private static CheckBox readOnlyCheckbox;
        #endregion

        #region Native functions

        // The callback called by the system when a window change occurs.
        static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            // Get the process for the window that was switched to.
            GetWindowThreadProcessId(hwnd, out uint processID);
            var process = OpenProcess(0x00001000, false, (int)processID);

            // Get the full path for the process's image.
            var nameSize = 1024;
            var nameBuilder = new StringBuilder(nameSize);
            QueryFullProcessImageName(process, 0, nameBuilder, ref nameSize);
            string processPath = nameBuilder.ToString(0, nameSize);

            // Check the name of the image against those of BL2 and TPS.
            string processName = Path.GetFileNameWithoutExtension(processPath);
            BackgroundWorker brWorker = new BackgroundWorker();
            if (processName == "Borderlands2" || processName == "BorderlandsPreSequel")
            {
                // If BL2 or TPS were switched to, register our file reading and hotkey.
                bl2 = processName == "Borderlands2";
                //RegisterHotKey(handle, HOTKEY_ID, 0x000, VK_readOnlyHotkey);
                dT.Start();
            }
            else
            {
                // If another process was switched to, unregister our file reading hotkey and read-only hotkey.
                //UnregisterHotKey(handle, HOTKEY_ID);
                dT.Stop();
            }
        }

        private IntPtr OnHotkeyPressed(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // If this is not a hotkey event, or is not tagged as ours, ignore it.
            if (msg != 0x0312 || wParam.ToInt32() != HOTKEY_ID)
                return IntPtr.Zero;

            // Determine the hotkey pressed.
            switch ((uint)(((int)lParam >> 16) & 0xFFFF))
            {
                case VK_readOnlyHotkey:
                    toggleReadOnly(bl2);
                    break;
            }
            handled = true;
            return IntPtr.Zero;
        }

        #endregion

        #region Constructors
        public MainWindow()
        {
            InitializeComponent();
            ReadOnlyTextBox.Text = Properties.Settings.Default.ReadOnlyText;
            dT.Tick += dispatcherTimer_tick;
            dT.Interval = new TimeSpan(0, 0, 0, Properties.Settings.Default.ReadOnlyCheckupTime);
            Closing += new CancelEventHandler(MainWindow_closing);
            window = this;
            readOnlyCheckbox = SaveGameReadOnly;
        }
        #endregion

        #region Closers

        #region Main Window Close Handler
        private void MainWindow_closing(object sender, CancelEventArgs e)
        {
            var result =
                MessageBox.Show(
                    "Are you sure you want to close the program?\nClicking \"No\" will move the program to the task-bar at which point it can be opened again.", "Close?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                ShowInTaskbar = true;
                WindowState = WindowState.Minimized;
                nIcon.Icon = Properties.Resources.bl2icon_E3N_icon;
                nIcon.Visible = true;
                nIcon.Text = "Borderlands Read-Only Detector";
                nIcon.Click += new EventHandler(nIconClickHandler);
                ShowInTaskbar = false;
            }
            else
            {
                e.Cancel = false;
            }
        }
        #endregion

        #region Notify Icon Click Handler
        private void nIconClickHandler(object sender, EventArgs e)
        {
            this.Show();
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.Focus();
            this.ShowInTaskbar = true;

            nIcon.Visible = false;
        }
        #endregion

        #endregion

        #region Timer Ticks
        // This code  happens on every tick of the timer that is determined by the value of the IntegerUpDown
        private static void dispatcherTimer_tick(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("Checking for Read-Only!\n\n");

                // Get our currently loaded save using BLIO's GetWillowPlayerController function.
                BLIO.Object WillowPlayerController = getPlayerController();
                string property = WillowPlayerController.GetProperty("SaveGameName");
                #region Save Gaming
                // The actual save file
                WillowSaveGame saveGame = string.IsNullOrEmpty(property) ? new WillowSaveGame(bl2) : new WillowSaveGame(property,bl2);

                readOnlyCheckbox.IsChecked = saveGame.saveGameInReadOnly;
                // This obtains console output every 10 seconds to notice if the player has ever loaded in.
                if (!saveGame.saveGameInReadOnly) return;

                Console.WriteLine("Read-Only is Enabled!");

                // Code to compare the save file date
                DateTime onDiskSaveFileDate = saveGame.LastWriteTime();

                // This is the general number format of: yyyy / mm / dd / hh / mm / ss
                // This format is the one used in the LastSavedDate property on a PlayerGameSave

                dateFormatter = new BorderlandsDateFormat(
                    onDiskSaveFileDate.Year.ToString(),
                    onDiskSaveFileDate.Month.ToString(),
                    onDiskSaveFileDate.Day.ToString(),
                    onDiskSaveFileDate.Hour.ToString(),
                    onDiskSaveFileDate.Minute.ToString(),
                    onDiskSaveFileDate.Second.ToString(),
                    onDiskSaveFileDate.ToString("tt")
                );

                updateRecentSaveDate(dateFormatter);

                long onDiskSaveFormat = Convert.ToInt64(dateFormatter.getDateFormat());
                Dictionary<string, string> responseList = GetAll("PlayerSaveGame", "LastSavedDate");

                if (responseList.Count < 0)
                {
                    // If we don't know the date, just return because the math will be incorrect.
                    if (previousMemoryDate == 0)
                    {
                        return;
                    }
                }

                #endregion

                // This handles all of our dates that are / were loaded into the memory at the time.
                #region Memory Date Handling

                long dateDifference = 0;
                #region Current Memory Date
                if (responseList.Count > 0)
                {
                    // This is a list of all of our dates.
                    List<long> memorySaveDateList = new List<long>();

                    foreach (KeyValuePair<string, string> entry in responseList)
                    {
                        try
                        {
                            memorySaveDateList.Add(Convert.ToInt64(entry.Value));
                        }
                        catch (FormatException ex)
                        {
                            continue;
                        }
                    }

                    long highestDate = FindMaxValue(memorySaveDateList, x => x);

                    previousMemoryDate = highestDate;

                    dateDifference = highestDate -
                                     onDiskSaveFormat;
                }
                #endregion

                #region Last known memory date
                else if (responseList.Count == 0)
                {
                    long highest = Math.Max(previousMemoryDate, onDiskSaveFormat);
                    long lowest = Math.Min(previousMemoryDate, onDiskSaveFormat);
                    if (highest == 0 || lowest == 0)
                    {
                        return;
                    }
                    dateDifference = highest - lowest;
                    Console.WriteLine(highest + " - " + lowest + " = " + dateDifference);
                }
                #endregion



                // This happens when the game is saving AND the on disk date is equal to the date in memory (probably never going to happen but better safe than sorry).
                if (dateDifference <= 0)
                {
                    return;
                }

                if (dateDifference < Properties.Settings.Default.ReadOnlyDifference) return;


                RunCommand("say {0}", Properties.Settings.Default.ReadOnlyText);

                #endregion
            }
            catch (IOException ioexc)
            {
                // Ignore "Pipe Interrupted/Broken" exception
                if (ioexc.HResult.ToString("X") != "80131620")
                {
                    //Debugger.Break();
                }
            }
            catch (Exception ex)
            {
                Debugger.Break();
            }

            #endregion

        }

        #region Initialization
        // Our initilization when the window initilizes. 
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);


            // Set up the above handle variables. 
            handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(OnHotkeyPressed);


            // Listen for name change changes across all processes/threads on current desktop.
            SetWinEventHook(0x0003, 0x0003, IntPtr.Zero, procDelegate, 0, 0, 0);
        }
        #endregion

        #region Hotkey Methods
        // This toggles on and off read-only based on the hotkey
        private static void toggleReadOnly(bool bl2)
        {
            // Get most recent save file
            WillowSaveGame saveGame = new WillowSaveGame(bl2);

            saveGame.toggleReadOnly();
        }

        #endregion

        #region Convenience Methods
        // This returns the highest value in a List of a given type (generally long).
        public static long FindMaxValue<T>(List<T> list, Converter<T, long> projection)
        {
            if (list.Count == 0)
            {
                throw new InvalidOperationException("Empty list");
            }
            long maxValue = long.MinValue;
            foreach (T item in list)
            {
                long value = projection(item);
                if (value > maxValue)
                {
                    maxValue = value;
                }
            }
            return maxValue;
        }

        // This returns our current WillowPlayerController as used in BL2 / TPS.
        public static BLIO.Object getPlayerController()
        {
            return BLIO.Object.GetPlayerController();
        }

        #endregion

        #region Value Handlers

        #region Text Boxes

        // This matches the text to the setting
        private void readOnlyTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            Properties.Settings.Default.ReadOnlyText = ReadOnlyTextBox.Text;
            Properties.Settings.Default.Save();
        }

        // This updates the date of the most recent save to the text-box value.
        private static void updateRecentSaveDate(BorderlandsDateFormat dateFormat)
        {
            window.MostRecentSaveDate.Text = dateFormat.ToString();
        }

        // This updates the hotkey used for read-only.
        /*private void ReadOnlyHotkey_Changed(object sender, TextChangedEventArgs e)
        {
            if (ReadOnlyHotkeyBox.Text.Length < 1) return;
            Properties.Settings.Default.ReadOnlyHotkey = ReadOnlyHotkeyBox.Text[0];
            Properties.Settings.Default.Save();
        }*/

        #endregion

        #region IntegerUpDowns
        // This handles the integer up down value for the read-only checkup time
        private void IntegerUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (IntegerUpDown.Value == null || IntegerUpDown.Value == 0) return;
            if (IntegerUpDown.Value == 0)
            {
                IntegerUpDown.Value = 1;
            }
            Properties.Settings.Default.ReadOnlyCheckupTime = (int)IntegerUpDown.Value;
            Properties.Settings.Default.Save();
            int timerValue = (int)IntegerUpDown.Value;
            dT.Interval = new TimeSpan(0, 0, 0, timerValue);
        }

        // This handles our time difference up down.
        private void differenceUpDown_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DifferenceUpDown.Value == null) return;

            Properties.Settings.Default.ReadOnlyDifference = (int)DifferenceUpDown.Value;
            Properties.Settings.Default.Save();
        }
        #endregion

        #endregion

    }

}
