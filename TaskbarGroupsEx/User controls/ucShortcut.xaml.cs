using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskbarGroupsEx.Classes;
using TaskbarGroupsEx.Forms;
using TaskbarGroupsEx.GroupItems;

namespace TaskbarGroupsEx.User_Controls
{
    public partial class ucShortcut : UserControl
    {
        public DynamicGroupItem? GroupItem = null;
        public frmMain? MotherForm = null;
        public FolderGroupConfig? ThisCategory = null;

        // Icon size customization
        private int _iconSize = 24;
        private int _iconSpacing = 55;

        // Flag to indicate if this icon is last in its row (no right margin needed)
        public bool IsLastInRow = false;

        public int IconSize
        {
            get => _iconSize;
            set
            {
                _iconSize = value;
                ApplyIconSize();
            }
        }

        public int IconSpacing
        {
            get => _iconSpacing;
            set
            {
                _iconSpacing = value;
                ApplyIconSize();
            }
        }

        public ucShortcut()
        {
            InitializeComponent();
        }

        private void ApplyIconSize()
        {
            // Update icon dimensions
            picIcon.Width = _iconSize;
            picIcon.Height = _iconSize;

            // Update selection cursor (slightly larger for glow effect)
            selectionCursor.Width = _iconSize + 4;
            selectionCursor.Height = _iconSize + 4;

            // Update container grid
            var grid = picIcon.Parent as System.Windows.Controls.Grid;
            if (grid != null)
            {
                grid.Width = _iconSize + 8;
                grid.Height = _iconSize + 8;
            }

            // Icon width = size + padding for hover glow (no spacing in width)
            this.Width = _iconSize + 8;
            this.Height = _iconSize + 14;
            // Spacing is the gap between icons - applied as right margin (except for last in row)
            int rightMargin = IsLastInRow ? 0 : _iconSpacing;
            this.Margin = new Thickness(0, 0, rightMargin, 0);
        }

        private void ucShortcut_Load(object sender, RoutedEventArgs e)
        {
            if (ThisCategory != null && GroupItem != null)
            {
                selectionCursor.Source = picIcon.Source = GroupItem.GetIcon();

                // Apply icon size and spacing from category config
                if (ThisCategory.IconSize > 0)
                {
                    _iconSize = ThisCategory.IconSize;
                }
                // Always apply spacing (0 is valid - means tightest packing)
                _iconSpacing = ThisCategory.IconSpacing;
                ApplyIconSize();
            }
        }

        private void ucShortcut_Click(object sender, MouseButtonEventArgs e)
        {
            ucShortcut_OnClick();
        }

        public void ucShortcut_OnClick()
        {
            if (GroupItem != null)
                GroupItem.OnExecute();
        }

        private void ucShortcut_MouseEnter(object sender, MouseEventArgs e)
        {
            ucShortcut_OnMouseEnter();
        }

        public void ucShortcut_OnMouseEnter()
        {
            selectionCursor.Visibility = Visibility.Visible;
        }

        private void ucShortcut_MouseLeave(object sender, MouseEventArgs e)
        {
            ucShortcut_OnMouseLeave();
        }

        public void ucShortcut_OnMouseLeave()
        {
            selectionCursor.Visibility = Visibility.Hidden;
        }
    }
}
