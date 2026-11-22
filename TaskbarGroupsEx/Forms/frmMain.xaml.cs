using Interop.UIAutomationClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskbarGroupsEx.Classes;
using TaskbarGroupsEx.GroupItems;
using TaskbarGroupsEx.User_Controls;

namespace TaskbarGroupsEx.Forms
{
    /// <summary>
    /// Interaction logic for frmMain.xaml
    /// </summary>
    public partial class frmMain : Window
    {
        public FolderGroupConfig? fgConfig;
        public List<ucShortcut> ControlList = new List<ucShortcut>();
        public System.Windows.Media.Color HoverColor;

        private string mShortcutName;
        private string mPath;
        private bool _closeOnDeactivate;

        // Must match FolderGroupConfig.InstallShortcut AppUserModelID
        private static string MenuAppUserModelId(string shortcutName) =>
            "TaskbarGroupEx.Menu." + shortcutName;

        public double Right
        {
            get { return this.Left + this.Width; }
        }

        //------------------------------------------------------------------------------------
        // CTOR AND LOAD
        //
        public frmMain(string ShortcutName)
        {
            NativeMethods.SetCurrentProcessExplicitAppUserModelID(MenuAppUserModelId(ShortcutName));
            System.Runtime.ProfileOptimization.StartProfile("frmMain.Profile");

            InitializeComponent();

            mShortcutName = ShortcutName;
            mPath = MainPath.Config + mShortcutName;
            this.WindowStyle = WindowStyle.None;
            this.Topmost = true;


            if (Directory.Exists(mPath))
            {
                this.Icon = ImageFunctions.IconPathToBitmapSource(mPath + "\\GroupIcon.ico");

                ControlList = new List<ucShortcut>();
                fgConfig = FolderGroupConfig.ParseConfiguration(mPath);
                bdrMain.Background = new SolidColorBrush(fgConfig.CatagoryBGColor);
                System.Windows.Media.Color BorderColor = System.Windows.Media.Color.FromArgb(fgConfig.CatagoryBGColor.A, 37, 37, 37);
                bdrMain.BorderBrush = new SolidColorBrush(BorderColor);
                
                HoverColor = System.Windows.Media.Color.Multiply(fgConfig.CatagoryBGColor, 3.0f);
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            NativeMethods.WindowsUXHelper.ApplyWindowsImmersion(this);
        }

        private void frmMain_Load(object sender, RoutedEventArgs e)
        {
            LoadCategory();
            SetLocation();

            // Taskbar launches can fire Deactivated before the popup finishes activating.
            Dispatcher.BeginInvoke(() => _closeOnDeactivate = true, DispatcherPriority.ApplicationIdle);
        }

        private void SetLocation()
        {
            Point mousePos = NativeMethods.GetMousePosition();
            double popupWidth = pnlShortcutIcons.Width;
            double popupHeight = pnlShortcutIcons.Height;
            int offsetX = fgConfig?.PopupXOffset ?? 0;
            int offsetY = fgConfig?.PopupYOffset ?? 0;

            if (TrySetLocationFromTaskbarButton(mousePos, popupWidth, popupHeight, offsetX, offsetY))
                return;

            // Fallback: center on cursor horizontally, sit above the cursor (taskbar click Y
            // would otherwise place most of the popup under/off the bottom of the screen).
            this.Left = mousePos.X - (popupWidth / 2) + offsetX;
            this.Top = mousePos.Y - popupHeight + offsetY;
        }

        private bool TrySetLocationFromTaskbarButton(Point mousePos, double popupWidth, double popupHeight, int offsetX, int offsetY)
        {
            try
            {
                IUIAutomation uia = new CUIAutomation();
                string modernId = MenuAppUserModelId(mShortcutName);
                string legacyId = "tjackenpacken.taskbarGroup.menu." + mShortcutName;

                tagRECT? bestRect = null;
                double bestDistance = double.MaxValue;

                foreach (string trayClass in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
                {
                    IntPtr hWndTray = NativeMethods.FindWindow(trayClass, null);
                    if (hWndTray == IntPtr.Zero)
                        continue;

                    // Win11 exposes taskbar buttons under the tray / XAML bridge, not MSTaskListWClass.
                    IUIAutomationElement? trayElement = uia.ElementFromHandle(hWndTray);
                    if (trayElement == null)
                        continue;

                    IUIAutomationElementArray elementArray = trayElement.FindAll(
                        TreeScope.TreeScope_Descendants,
                        uia.CreateTrueCondition());

                    for (int i = 0; i < elementArray.Length; i++)
                    {
                        IUIAutomationElement element = elementArray.GetElement(i);
                        string automationId = element.CurrentAutomationId ?? string.Empty;
                        string name = element.CurrentName ?? string.Empty;

                        bool matches =
                            automationId.Contains(modernId, StringComparison.OrdinalIgnoreCase) ||
                            automationId.Contains(legacyId, StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith(mShortcutName + " pinned", StringComparison.OrdinalIgnoreCase);

                        if (!matches)
                            continue;

                        tagRECT rect = element.CurrentBoundingRectangle;
                        if (rect.right <= rect.left || rect.bottom <= rect.top)
                            continue;

                        double centerX = (rect.left + rect.right) / 2.0;
                        double centerY = (rect.top + rect.bottom) / 2.0;
                        double dx = centerX - mousePos.X;
                        double dy = centerY - mousePos.Y;
                        double distance = (dx * dx) + (dy * dy);

                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestRect = rect;
                        }
                    }
                }

                if (bestRect is tagRECT match)
                {
                    this.Left = match.left + ((match.right - match.left) / 2.0) - (popupWidth / 2.0) + offsetX;
                    this.Top = match.top - popupHeight + offsetY;
                    return true;
                }
            }
            catch
            {
                // Fall through to mouse-based placement.
            }

            return false;
        }
        //
        //------------------------------------------------------------------------------------
        //

        // Loading category and building shortcuts
        private void LoadCategory()
        {
            if (fgConfig == null)
                return;

            if (!Directory.Exists(@MainPath.Config + fgConfig.GetName() + @"\Icons\"))
            {
                fgConfig.SaveIcons();
            }

            int totalIcons = fgConfig.GroupItemList.Count;
            int maxColumns = fgConfig.CollumnCount;
            // Actual columns = min of total icons and max columns setting
            int actualColumns = Math.Min(totalIcons, maxColumns);
            double rowCount = Math.Ceiling((double)totalIcons / maxColumns);

            int iconSize = fgConfig.IconSize > 0 ? fgConfig.IconSize : 24;
            int spacing = fgConfig.IconSpacing;  // Gap between icons (0 = touching)

            // Icon width = size + padding for hover glow
            int iconWidth = iconSize + 8;
            // Panel width = actual icons + gaps between them (no trailing gap)
            int panelWidth = (actualColumns * iconWidth) + ((actualColumns - 1) * spacing);
            // Height: rows with padding
            pnlShortcutIcons.Height = rowCount * (iconSize + 14) + 4;
            pnlShortcutIcons.Width = panelWidth;

            for (int i = 0; i < fgConfig.GroupItemList.Count; i++)
            {
                DynamicGroupItem groupItem = fgConfig.GroupItemList[i];

                // Check if this icon is last in its row (no right margin needed)
                bool isLastInRow = ((i + 1) % actualColumns == 0) || (i == fgConfig.GroupItemList.Count - 1);

                // Building shortcut controls
                ucShortcut pscPanel = new ucShortcut()
                {
                    GroupItem = groupItem,
                    MotherForm = this,
                    ThisCategory = fgConfig,
                    IsLastInRow = isLastInRow,
                };

                pnlShortcutIcons.Children.Add(pscPanel);
                this.ControlList.Add(pscPanel);
            }
        }

        // Click handler for shortcuts
        public void OpenFile(string arguments, string path, string workingDirec)
        {
            // starting program from psc panel click
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.Arguments = arguments;
            proc.FileName = path;
            proc.WorkingDirectory = workingDirec;
            proc.UseShellExecute = true;

            try
            {
                Process.Start(proc);
            }
            catch (Exception Ex)
            {
                MessageBox.Show(Ex.Message);
            }
        }

        // Closes application upon deactivation
        private void frmMain_Deactivate(object sender, EventArgs e)
        {
            // closes program if user clicks outside form (after startup settle)
            if (_closeOnDeactivate)
                this.Close();
        }

        // Keyboard shortcut handlers
        private void frmMain_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key >= Key.D1 && e.Key <= Key.D0)
                {
                    int idx = e.Key - Key.D1;
                    ControlList[idx].ucShortcut_OnMouseEnter();
                }
            }
            catch{}
        }

        private void frmMain_KeyUp(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Enter && fgConfig != null && fgConfig.allowOpenAll)
            {
                foreach (ucShortcut usc in this.ControlList)
                    usc.ucShortcut_OnClick();
            }

            try
            {
                if (e.Key >= Key.D1 && e.Key <= Key.D0)
                {
                    int idx = e.Key - Key.D1;
                    ControlList[idx].ucShortcut_OnMouseLeave();
                    ControlList[idx].ucShortcut_OnClick();
                }
            }              
            catch{}
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            this.Visibility = Visibility.Visible;
            NativeMethods.GlobalActivate(this);
        }

        //
        // END OF CLASS
        //
    }
}
