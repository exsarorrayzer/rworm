using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RWORM.GUI.Models;
using RWORM.GUI.ViewModels;

namespace RWORM.GUI
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private Point _selectionStartPoint;
        private bool _isMouseDown;
        private bool _isMarqueeSelecting;
        private readonly HashSet<ClientModel> _preSelectedClients = new();

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
            Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            _vm.KillServer();
        }

        /// Sync DataGrid multi-selection → ViewModel.SelectedClients
        private void ClientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SelectedClients.Clear();
            foreach (ClientModel item in ClientsGrid.SelectedItems.OfType<ClientModel>())
                _vm.SelectedClients.Add(item);
        }

        // ── Mouse-drag marquee selection (Windows-like blue box) ───────────────
        private void ClientsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (FindVisualParent<ScrollBar>(originalSource) != null ||
                FindVisualParent<DataGridColumnHeader>(originalSource) != null ||
                FindVisualParent<TextBox>(originalSource) != null)
            {
                return;
            }

            _selectionStartPoint = e.GetPosition(ClientsGridContainer);
            _isMouseDown = true;
            _isMarqueeSelecting = false;

            _preSelectedClients.Clear();
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                foreach (var item in ClientsGrid.SelectedItems.OfType<ClientModel>())
                    _preSelectedClients.Add(item);
            }
        }

        private void ClientsGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown || e.LeftButton != MouseButtonState.Pressed) return;

            var cur = e.GetPosition(ClientsGridContainer);
            if (!_isMarqueeSelecting)
            {
                if (Math.Abs(cur.X - _selectionStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(cur.Y - _selectionStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isMarqueeSelecting = true;
                    ClientsGrid.CaptureMouse();
                    SelectionBox.Visibility = Visibility.Visible;
                }
            }

            if (_isMarqueeSelecting)
            {
                double x = Math.Min(_selectionStartPoint.X, cur.X);
                double y = Math.Min(_selectionStartPoint.Y, cur.Y);
                double w = Math.Abs(cur.X - _selectionStartPoint.X);
                double h = Math.Abs(cur.Y - _selectionStartPoint.Y);

                Canvas.SetLeft(SelectionBox, x);
                Canvas.SetTop(SelectionBox, y);
                SelectionBox.Width  = w;
                SelectionBox.Height = h;

                Rect boxRect = new Rect(x, y, w, h);
                UpdateMarqueeSelection(boxRect);
            }
        }

        private void UpdateMarqueeSelection(Rect boxRect)
        {
            bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            for (int i = 0; i < ClientsGrid.Items.Count; i++)
            {
                var row = (DataGridRow)ClientsGrid.ItemContainerGenerator.ContainerFromIndex(i);
                if (row == null) continue;

                if (ClientsGrid.Items[i] is not ClientModel client) continue;

                try
                {
                    var transform = row.TransformToAncestor(ClientsGridContainer);
                    var rowRect = transform.TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));

                    if (boxRect.IntersectsWith(rowRect))
                    {
                        if (!row.IsSelected) row.IsSelected = true;
                    }
                    else
                    {
                        if (isCtrl)
                        {
                            row.IsSelected = _preSelectedClients.Contains(client);
                        }
                        else
                        {
                            if (row.IsSelected) row.IsSelected = false;
                        }
                    }
                }
                catch { }
            }
        }

        private void ClientsGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMarqueeSelecting)
            {
                _isMarqueeSelecting = false;
                SelectionBox.Visibility = Visibility.Collapsed;
                ClientsGrid.ReleaseMouseCapture();
                e.Handled = true;
            }
            _isMouseDown = false;
        }

        private void ClientsGrid_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isMarqueeSelecting)
            {
                _isMarqueeSelecting = false;
                SelectionBox.Visibility = Visibility.Collapsed;
            }
            _isMouseDown = false;
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
