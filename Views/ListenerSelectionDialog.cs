using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Models;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Contract2512.Views
{
    internal sealed class ListenerSelectionDialog : FluentWindow
    {
        private readonly List<SelectableContractItem> _items;
        private readonly bool _isGroupMode;

        public List<Contract> SelectedContracts { get; private set; } = new();

        public ListenerSelectionDialog(List<Contract> availableContracts, List<Contract> selectedContracts, bool isGroupMode)
        {
            _isGroupMode = isGroupMode;
            _items = availableContracts.Select(contract => new SelectableContractItem
            {
                Contract = contract,
                IsSelected = selectedContracts.Any(selected => selected.Id == contract.Id)
            }).ToList();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = _isGroupMode ? "Выбор слушателей группы" : "Выбор слушателя";
            Width = 700;
            Height = 620;
            MinWidth = 640;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ExtendsContentIntoTitleBar = true;

            var root = DialogThemeHelper.CreateWindowRoot(this, Title, showMaximizeButton: false);
            var contentBorder = DialogThemeHelper.CreateContentBorder();
            Grid.SetRow(contentBorder, 1);
            root.Children.Add(contentBorder);

            var contentGrid = new Grid { Margin = new Thickness(20) };
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            contentGrid.Children.Add(new TextBlock
            {
                Text = _isGroupMode
                    ? "Отметьте всех слушателей, для которых нужно сформировать отдельные документы."
                    : "Выберите одного слушателя для индивидуального режима.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(0, 0, 0, 14)
            });

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scrollViewer, 1);
            contentGrid.Children.Add(scrollViewer);

            var itemsPanel = new StackPanel();
            foreach (var item in _items)
            {
                string listenerName = item.Contract.Listener?.FullName ?? $"Слушатель #{item.Contract.ListenerId}";
                string contractNumber = item.Contract.ContractNumber ?? "без номера";
                string start = item.Contract.StartDate?.ToString("dd.MM.yyyy") ?? "без даты";
                string end = item.Contract.EndDate?.ToString("dd.MM.yyyy") ?? "без даты";

                var checkBox = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    Margin = new Thickness(0, 0, 0, 10),
                    Foreground = System.Windows.Media.Brushes.White,
                    Content = $"{listenerName} | договор {contractNumber} | {start} - {end}"
                };

                checkBox.Checked += (_, _) =>
                {
                    if (!_isGroupMode)
                    {
                        foreach (var other in itemsPanel.Children.OfType<CheckBox>())
                        {
                            if (!ReferenceEquals(other, checkBox))
                            {
                                other.IsChecked = false;
                            }
                        }
                    }

                    item.IsSelected = true;
                };

                checkBox.Unchecked += (_, _) => item.IsSelected = false;
                itemsPanel.Children.Add(checkBox);
            }

            scrollViewer.Content = itemsPanel;

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);
            contentGrid.Children.Add(buttonPanel);

            var okButton = DialogThemeHelper.CreateDialogButton("Выбрать", accent: true);
            okButton.Margin = new Thickness(0, 0, 10, 0);
            okButton.Click += (_, _) =>
            {
                SelectedContracts = _items.Where(i => i.IsSelected).Select(i => i.Contract).ToList();

                if (SelectedContracts.Count == 0)
                {
                    MessageBox.Show(_isGroupMode ? "Выберите хотя бы одного слушателя." : "Выберите слушателя.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_isGroupMode && SelectedContracts.Count > 1)
                {
                    MessageBox.Show("В индивидуальном режиме можно выбрать только одного слушателя.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            };

            var cancelButton = DialogThemeHelper.CreateDialogButton("Отмена");
            cancelButton.Click += (_, _) =>
            {
                DialogResult = false;
                Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            contentBorder.Child = contentGrid;
            Content = root;
        }

        private sealed class SelectableContractItem
        {
            public Contract Contract { get; init; } = null!;
            public bool IsSelected { get; set; }
        }
    }
}
