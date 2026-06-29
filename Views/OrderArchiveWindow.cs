using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Contract2512.Models;
using Contract2512.Services;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public sealed class OrderArchiveWindow : FluentWindow
    {
        private readonly OrderDocumentService _orderService = new();
        private readonly DataGrid _documentsGrid;

        public OrderArchiveWindow()
        {
            Title = "Архив приказов";
            Width = 1200;
            Height = 720;
            MinWidth = 900;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ExtendsContentIntoTitleBar = true;
            Background = CreateAppBackground();

            _documentsGrid = CreateDocumentsGrid();
            Content = BuildLayout();
            RefreshDocuments();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid
            {
                Background = CreateAppBackground()
            };

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = CreateTitleBar();
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var header = new StackPanel
            {
                Margin = new Thickness(20, 20, 20, 14)
            };

            header.Children.Add(new TextBlock
            {
                Text = "Архив приказов",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });

            header.Children.Add(new TextBlock
            {
                Text = "Здесь хранятся приказы, скрытые из основной истории.",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(0, 6, 0, 0)
            });

            Grid.SetRow(header, 1);
            root.Children.Add(header);

            var content = new Grid
            {
                Margin = new Thickness(20, 0, 20, 20)
            };

            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            buttons.Children.Add(CreateButton("Открыть файл", OpenSelectedDocument));
            buttons.Children.Add(CreateButton("Вернуть приказ", RestoreSelectedDocument));
            buttons.Children.Add(CreateButton("Обновить", RefreshDocuments));

            Grid.SetRow(buttons, 0);
            content.Children.Add(buttons);

            var gridBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(76, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.45,
                    BlurRadius = 25,
                    ShadowDepth = 4
                },
                Child = _documentsGrid
            };

            Grid.SetRow(gridBorder, 1);
            content.Children.Add(gridBorder);

            Grid.SetRow(content, 2);
            root.Children.Add(content);

            return root;
        }

        private UIElement CreateTitleBar()
        {
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(49, 46, 129)),
                Height = 30
            };

            titleBar.MouseDown += TitleBar_MouseDown;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Архив приказов",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                FontSize = 14
            };

            Grid.SetColumn(title, 0);
            grid.Children.Add(title);

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            controls.Children.Add(CreateWindowControlButton("-", MinimizeButton_Click));
            controls.Children.Add(CreateWindowControlButton("×", CloseButton_Click, true));

            Grid.SetColumn(controls, 1);
            grid.Children.Add(controls);

            titleBar.Child = grid;
            return titleBar;
        }

        private void RefreshDocuments()
        {
            try
            {
                _documentsGrid.ItemsSource = _orderService.GetArchivedDocuments();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке архива приказов: {ex.Message}");
            }
        }

        private void OpenSelectedDocument()
        {
            if (_documentsGrid.SelectedItem is not OrderDocument document)
            {
                ShowWarning("Выберите приказ из архива.");
                return;
            }

            if (!_orderService.TryResolveDocumentPath(document, out var documentPath))
            {
                ShowWarning("Файл не найден. Возможно, он был удален или перемещен.");
                return;
            }

            try
            {
                _orderService.OpenDocument(documentPath);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при открытии файла: {ex.Message}");
            }
        }

        private void RestoreSelectedDocument()
        {
            if (_documentsGrid.SelectedItem is not OrderDocument document)
            {
                ShowWarning("Выберите приказ для возврата из архива.");
                return;
            }

            var result = MessageBox.Show(
                $"Вернуть приказ \"{document.OrderName}\" из архива?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _orderService.RestoreDocument(document.Id);
                RefreshDocuments();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при возврате приказа из архива: {ex.Message}");
            }
        }

        private static Button CreateButton(string text, Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(77, 75, 85, 99)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                FocusVisualStyle = null,
                HorizontalAlignment = HorizontalAlignment.Left,
                Template = CreateRoundedButtonTemplate(8)
            };

            button.MouseEnter += (_, _) =>
                button.Background = new SolidColorBrush(Color.FromArgb(204, 107, 114, 128));
            button.MouseLeave += (_, _) =>
                button.Background = new SolidColorBrush(Color.FromArgb(77, 75, 85, 99));
            button.Click += (_, _) => onClick();

            return button;
        }

        private static Button CreateWindowControlButton(string text, RoutedEventHandler onClick, bool isCloseButton = false)
        {
            var button = new Button
            {
                Content = text,
                Width = 46,
                Height = 30,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13,
                FocusVisualStyle = null,
                Template = CreateFlatButtonTemplate()
            };

            button.MouseEnter += (_, _) =>
            {
                button.Background = isCloseButton
                    ? new SolidColorBrush(Color.FromArgb(204, 239, 68, 68))
                    : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            };
            button.MouseLeave += (_, _) => button.Background = Brushes.Transparent;
            button.Click += onClick;

            return button;
        }

        private static ControlTemplate CreateRoundedButtonTemplate(double cornerRadius)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(content);

            return new ControlTemplate(typeof(Button))
            {
                VisualTree = border
            };
        }

        private static ControlTemplate CreateFlatButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(content);

            return new ControlTemplate(typeof(Button))
            {
                VisualTree = border
            };
        }

        private static DataGrid CreateDocumentsGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                EnableColumnVirtualization = true,
                EnableRowVirtualization = true,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                RowBackground = new SolidColorBrush(Color.FromArgb(76, 30, 41, 59)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromArgb(76, 15, 23, 42)),
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeaderWidth = 0,
                ColumnHeaderStyle = CreateColumnHeaderStyle(),
                CellStyle = CreateCellStyle(),
                RowStyle = CreateRowStyle()
            };

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "В архиве с",
                Binding = new Binding("ArchivedAt") { StringFormat = "dd.MM.yyyy HH:mm" },
                Width = 150
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Дата создания",
                Binding = new Binding("GeneratedAt") { StringFormat = "dd.MM.yyyy" },
                Width = 150
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Тип",
                Binding = new Binding("OrderName"),
                Width = 220
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Программа",
                Binding = new Binding("Program.Name"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Слушатель",
                Binding = new Binding("Listener.FullName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Преподаватель",
                Binding = new Binding("Teacher.FullName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Файл",
                Binding = new Binding("FileName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            return grid;
        }

        private static Style CreateColumnHeaderStyle()
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(85, 51, 65, 85))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 0)));
            return style;
        }

        private static Style CreateCellStyle()
        {
            var style = new Style(typeof(DataGridCell));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(71, 85, 105))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

            var selected = new Trigger
            {
                Property = DataGridCell.IsSelectedProperty,
                Value = true
            };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(115, 99, 102, 241))));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            selected.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(129, 140, 248))));
            style.Triggers.Add(selected);

            return style;
        }

        private static Style CreateRowStyle()
        {
            var style = new Style(typeof(DataGridRow));

            var hover = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(85, 51, 65, 85))));

            var selected = new Trigger
            {
                Property = Selector.IsSelectedProperty,
                Value = true
            };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(115, 99, 102, 241))));

            style.Triggers.Add(hover);
            style.Triggers.Add(selected);

            return style;
        }

        private static Brush CreateAppBackground()
        {
            return new LinearGradientBrush(
                Color.FromRgb(30, 27, 75),
                Color.FromRgb(15, 23, 42),
                new Point(0, 0),
                new Point(1, 1));
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(Contract2512.Services.UserErrorMessageService.ToRussianText(message), "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
