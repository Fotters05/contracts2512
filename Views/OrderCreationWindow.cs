using System.Windows;
using System.Windows.Controls;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Contract2512.Views
{
    public sealed class OrderCreationWindow : FluentWindow
    {
        public OrderCreationWindow()
        {
            Title = "Создание приказа";
            Width = 1280;
            Height = 860;
            MinWidth = 1080;
            MinHeight = 760;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ExtendsContentIntoTitleBar = true;

            var root = DialogThemeHelper.CreateWindowRoot(this, Title, showMaximizeButton: false);
            var border = DialogThemeHelper.CreateContentBorder();
            border.Padding = new Thickness(20);
            Grid.SetRow(border, 1);
            root.Children.Add(border);

            var control = new OrderCreationControl();
            control.OrderCreated += (_, _) =>
            {
                DialogResult = true;
                Close();
            };

            border.Child = control;
            Content = root;
        }
    }
}
