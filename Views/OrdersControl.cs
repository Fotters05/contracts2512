using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Contract2512.Models;
using Contract2512.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public sealed class OrdersControl : UserControl
    {
        private readonly OrderDocumentService _orderService = new();
        private readonly DataGrid _documentsGrid;

        public OrdersControl()
        {
            _documentsGrid = CreateDocumentsGrid();
            Content = BuildLayout();
            RefreshDocuments();
        }

        public void NotifyPanelShown()
        {
            RefreshDocuments();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerCard = CreateCard(
                "Приказы",
                CreateDescription("Здесь хранится история уже сформированных приказов. Для нового документа открой отдельное окно создания."),
                BuildButtons());
            Grid.SetRow(headerCard, 0);
            root.Children.Add(headerCard);

            var historyCard = CreateStretchCard("История приказов", _documentsGrid);
            Grid.SetRow(historyCard, 1);
            root.Children.Add(historyCard);

            return root;
        }

        private UIElement BuildButtons()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };

            panel.Children.Add(CreateButton("Создать приказ", OpenCreationWindow));
            panel.Children.Add(CreateButton("Открыть файл", OpenSelectedDocument));
            panel.Children.Add(CreateButton("Обновить список", RefreshDocuments));

            panel.Children.Insert(1, CreateButton("Реестр приказов", OpenRegistryWindow));

            return panel;
        }

        private void OpenCreationWindow()
        {
            var window = new OrderCreationWindow
            {
                Owner = Window.GetWindow(this)
            };

            if (window.ShowDialog() == true)
            {
                RefreshDocuments();
            }
        }

        private void OpenRegistryWindow()
        {
            var window = new OrderRegistryWindow
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();
        }

        private void RefreshDocuments()
        {
            try
            {
                _documentsGrid.ItemsSource = _orderService.GetGeneratedDocuments();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при загрузке истории приказов: {ex.Message}");
            }
        }

        private void OpenSelectedDocument()
        {
            if (_documentsGrid.SelectedItem is not OrderDocument document)
            {
                ShowWarning("Выберите документ из истории.");
                return;
            }

            if (!_orderService.TryResolveDocumentPath(document, out var documentPath))
            {
                ShowWarning("Файл не найден. Возможно, он был удалён или перемещён.");
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

        private static Border CreateCard(string title, params UIElement[] children)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            });

            foreach (var child in children)
            {
                panel.Children.Add(child);
            }

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(76, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(20),
                Child = panel
            };
        }

        private static Border CreateStretchCard(string title, UIElement child)
        {
            var panel = new Grid();
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            };

            Grid.SetRow(titleBlock, 0);
            panel.Children.Add(titleBlock);

            Grid.SetRow(child, 1);
            panel.Children.Add(child);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(76, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 16),
                Padding = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = panel
            };
        }

        private static TextBlock CreateDescription(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(0, 0, 0, 10)
            };
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
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            button.Click += (_, _) => onClick();
            return button;
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
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                EnableColumnVirtualization = true,
                EnableRowVirtualization = true
            };

            grid.SetResourceReference(StyleProperty, "DarkDataGridStyle");
            grid.SetResourceReference(DataGrid.ColumnHeaderStyleProperty, "DarkDataGridColumnHeaderStyle");
            grid.SetResourceReference(DataGrid.CellStyleProperty, "DarkDataGridCellStyle");
            grid.SetResourceReference(DataGrid.RowStyleProperty, "DarkDataGridRowStyle");

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Дата",
                Binding = new System.Windows.Data.Binding("GeneratedAt") { StringFormat = "dd.MM.yyyy HH:mm" },
                Width = 150
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Тип",
                Binding = new System.Windows.Data.Binding("OrderName"),
                Width = 220
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Программа",
                Binding = new System.Windows.Data.Binding("Program.Name"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Слушатель",
                Binding = new System.Windows.Data.Binding("Listener.FullName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Преподаватель",
                Binding = new System.Windows.Data.Binding("Teacher.FullName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Файл",
                Binding = new System.Windows.Data.Binding("FileName"),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            return grid;
        }

        private static void ShowWarning(string message)
        {
            MessageBox.Show(message, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
