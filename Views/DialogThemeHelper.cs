using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Contract2512.Views
{
    internal static class DialogThemeHelper
    {
        public static void ApplySharedResources(FrameworkElement element)
        {
            if (element.Resources.MergedDictionaries.Any(dictionary => dictionary.Contains("OrderDialogThemeMarker")))
            {
                return;
            }

            var dictionary = (ResourceDictionary)XamlReader.Parse(
                """
                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <x:String x:Key="OrderDialogThemeMarker">OrderDialogTheme</x:String>

                    <LinearGradientBrush x:Key="AppBackground" StartPoint="0 0" EndPoint="1 1">
                        <GradientStop Color="#1E1B4B" Offset="0"/>
                        <GradientStop Color="#0F172A" Offset="1"/>
                    </LinearGradientBrush>
                    <SolidColorBrush x:Key="TitleBarBackground" Color="#312E81"/>
                    <SolidColorBrush x:Key="TextPrimary" Color="White"/>
                    <SolidColorBrush x:Key="TextSecondary" Color="#D1D5DB"/>
                    <SolidColorBrush x:Key="BorderColor" Color="#475569"/>

                    <Style x:Key="DarkTextBoxStyle" TargetType="TextBox">
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#1E293B" Opacity="0.4"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="BorderBrush" Value="{StaticResource BorderColor}"/>
                        <Setter Property="BorderThickness" Value="1"/>
                        <Setter Property="Padding" Value="8,6"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="CaretBrush" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="TextBox">
                                    <Border Background="{TemplateBinding Background}"
                                            BorderBrush="{TemplateBinding BorderBrush}"
                                            BorderThickness="{TemplateBinding BorderThickness}"
                                            CornerRadius="6"
                                            Padding="{TemplateBinding Padding}">
                                        <ScrollViewer x:Name="PART_ContentHost"
                                                      Background="Transparent"
                                                      Foreground="{TemplateBinding Foreground}"/>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="DarkComboBoxItemStyle" TargetType="ComboBoxItem">
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="BorderThickness" Value="0"/>
                        <Setter Property="Padding" Value="8,6"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="ComboBoxItem">
                                    <Border Background="{TemplateBinding Background}"
                                            BorderBrush="{StaticResource BorderColor}"
                                            BorderThickness="0,0,0,1"
                                            Padding="{TemplateBinding Padding}">
                                        <ContentPresenter HorizontalAlignment="Left"
                                                          VerticalAlignment="Center"/>
                                    </Border>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsMouseOver" Value="True">
                                            <Setter Property="Background">
                                                <Setter.Value>
                                                    <SolidColorBrush Color="#334155" Opacity="0.5"/>
                                                </Setter.Value>
                                            </Setter>
                                        </Trigger>
                                        <Trigger Property="IsSelected" Value="True">
                                            <Setter Property="Background">
                                                <Setter.Value>
                                                    <SolidColorBrush Color="#4C46E5" Opacity="0.5"/>
                                                </Setter.Value>
                                            </Setter>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="DarkComboBoxStyle" TargetType="ComboBox">
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#1E293B" Opacity="0.4"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="BorderBrush" Value="{StaticResource BorderColor}"/>
                        <Setter Property="BorderThickness" Value="1"/>
                        <Setter Property="Padding" Value="8,6"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="ItemContainerStyle" Value="{StaticResource DarkComboBoxItemStyle}"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="ComboBox">
                                    <Grid>
                                        <Border Background="{TemplateBinding Background}"
                                                BorderBrush="{TemplateBinding BorderBrush}"
                                                BorderThickness="{TemplateBinding BorderThickness}"
                                                CornerRadius="6">
                                            <Grid>
                                                <Grid.ColumnDefinitions>
                                                    <ColumnDefinition/>
                                                    <ColumnDefinition Width="Auto"/>
                                                </Grid.ColumnDefinitions>
                                                <ContentPresenter Name="ContentSite"
                                                                  Grid.Column="0"
                                                                  IsHitTestVisible="False"
                                                                  Content="{TemplateBinding SelectionBoxItem}"
                                                                  ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                                                  ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}"
                                                                  Margin="8,3,25,3"
                                                                  VerticalAlignment="Center"
                                                                  HorizontalAlignment="Left"/>
                                                <TextBox Name="PART_EditableTextBox"
                                                         Grid.Column="0"
                                                         Style="{x:Null}"
                                                         HorizontalAlignment="Stretch"
                                                         VerticalAlignment="Center"
                                                         Margin="8,3,25,3"
                                                         Background="Transparent"
                                                         Foreground="{StaticResource TextPrimary}"
                                                         BorderThickness="0"
                                                         Visibility="Hidden"
                                                         IsReadOnly="{TemplateBinding IsReadOnly}"/>
                                                <ToggleButton Grid.Column="1"
                                                              Focusable="False"
                                                              ClickMode="Press"
                                                              Background="Transparent"
                                                              BorderThickness="0"
                                                              Width="38"
                                                              Cursor="Hand"
                                                              IsChecked="{Binding Path=IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                                                    <ToggleButton.Template>
                                                        <ControlTemplate TargetType="ToggleButton">
                                                            <Border Background="Transparent"
                                                                    Padding="10,0">
                                                                <Path HorizontalAlignment="Center"
                                                                      VerticalAlignment="Center"
                                                                      Data="M 0 0 L 4 4 L 8 0 Z"
                                                                      Fill="{StaticResource TextPrimary}"/>
                                                            </Border>
                                                        </ControlTemplate>
                                                    </ToggleButton.Template>
                                                </ToggleButton>
                                            </Grid>
                                        </Border>
                                        <Popup Name="Popup"
                                               Placement="Bottom"
                                               IsOpen="{TemplateBinding IsDropDownOpen}"
                                               AllowsTransparency="True"
                                               Focusable="False"
                                               PopupAnimation="Slide">
                                            <Border BorderBrush="{StaticResource BorderColor}"
                                                    BorderThickness="1"
                                                    CornerRadius="6"
                                                    MaxHeight="{TemplateBinding MaxDropDownHeight}"
                                                    MinWidth="{TemplateBinding ActualWidth}">
                                                <Border.Background>
                                                    <SolidColorBrush Color="#1E293B" Opacity="0.95"/>
                                                </Border.Background>
                                                <ScrollViewer Margin="4,6,4,6">
                                                    <StackPanel IsItemsHost="True"/>
                                                </ScrollViewer>
                                            </Border>
                                        </Popup>
                                    </Grid>
                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsEditable" Value="true">
                                            <Setter Property="IsHitTestVisible" TargetName="PART_EditableTextBox" Value="true"/>
                                            <Setter Property="Visibility" TargetName="PART_EditableTextBox" Value="Visible"/>
                                            <Setter Property="Visibility" TargetName="ContentSite" Value="Hidden"/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="DarkCalendarDayButtonStyle" TargetType="CalendarDayButton">
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#334155" Opacity="0.6"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="BorderThickness" Value="1"/>
                    </Style>

                    <Style x:Key="DarkCalendarButtonStyle" TargetType="CalendarButton">
                        <Setter Property="Foreground" Value="White"/>
                        <Setter Property="FontWeight" Value="SemiBold"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="BorderThickness" Value="0"/>
                    </Style>

                    <Style x:Key="DarkCalendarStyle" TargetType="Calendar">
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#334155" Opacity="0.95"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Foreground" Value="White"/>
                        <Setter Property="BorderBrush" Value="#64748B"/>
                        <Setter Property="BorderThickness" Value="1"/>
                        <Setter Property="CalendarDayButtonStyle" Value="{StaticResource DarkCalendarDayButtonStyle}"/>
                        <Setter Property="CalendarButtonStyle" Value="{StaticResource DarkCalendarButtonStyle}"/>
                    </Style>

                    <Style x:Key="DarkDatePickerStyle" TargetType="DatePicker">
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#1E293B" Opacity="0.4"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="BorderBrush" Value="{StaticResource BorderColor}"/>
                        <Setter Property="BorderThickness" Value="1"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="CalendarStyle" Value="{StaticResource DarkCalendarStyle}"/>
                    </Style>

                    <Style x:Key="DarkButtonStyle" TargetType="Button">
                        <Setter Property="Foreground" Value="White"/>
                        <Setter Property="Background">
                            <Setter.Value>
                                <SolidColorBrush Color="#4B5563" Opacity="0.6"/>
                            </Setter.Value>
                        </Setter>
                        <Setter Property="Padding" Value="12,6"/>
                        <Setter Property="FontWeight" Value="SemiBold"/>
                        <Setter Property="FontSize" Value="14"/>
                        <Setter Property="Cursor" Value="Hand"/>
                        <Setter Property="BorderThickness" Value="0"/>
                        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="Button">
                                    <Border Background="{TemplateBinding Background}"
                                            CornerRadius="6"
                                            Padding="{TemplateBinding Padding}">
                                        <ContentPresenter HorizontalAlignment="Center"
                                                          VerticalAlignment="Center"/>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="AccentButton" TargetType="Button" BasedOn="{StaticResource DarkButtonStyle}">
                        <Setter Property="Background" Value="#4C46E5"/>
                        <Setter Property="Padding" Value="16,10"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="Button">
                                    <Border Background="{TemplateBinding Background}"
                                            CornerRadius="8"
                                            Padding="{TemplateBinding Padding}">
                                        <ContentPresenter HorizontalAlignment="Center"
                                                          VerticalAlignment="Center"/>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="WindowControlButton" TargetType="Button">
                        <Setter Property="Width" Value="46"/>
                        <Setter Property="Height" Value="32"/>
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="BorderThickness" Value="0"/>
                        <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
                        <Setter Property="FontSize" Value="12"/>
                        <Setter Property="Cursor" Value="Hand"/>
                        <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
                        <Setter Property="Template">
                            <Setter.Value>
                                <ControlTemplate TargetType="Button">
                                    <Border Background="{TemplateBinding Background}">
                                        <ContentPresenter HorizontalAlignment="Center"
                                                          VerticalAlignment="Center"/>
                                    </Border>
                                </ControlTemplate>
                            </Setter.Value>
                        </Setter>
                    </Style>

                    <Style x:Key="CloseButtonStyle" TargetType="Button" BasedOn="{StaticResource WindowControlButton}">
                        <Style.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter Property="Background" Value="#E81123"/>
                                <Setter Property="Foreground" Value="White"/>
                            </Trigger>
                        </Style.Triggers>
                    </Style>
                </ResourceDictionary>
                """);

            element.Resources.MergedDictionaries.Add(dictionary);
        }

        public static Grid CreateWindowRoot(Window window, string title, bool showMaximizeButton)
        {
            ApplySharedResources(window);

            var root = new Grid();
            root.SetResourceReference(Panel.BackgroundProperty, "AppBackground");
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = CreateTitleBar(window, title, showMaximizeButton);
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            return root;
        }

        public static Border CreateContentBorder()
        {
            return new Border
            {
                Margin = new Thickness(20),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)) { Opacity = 0.32 },
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1)
            };
        }

        public static Button CreateDialogButton(string text, bool accent = false)
        {
            var button = new Button
            {
                Content = text,
                MinHeight = 40,
                Padding = accent ? new Thickness(16, 10, 16, 10) : new Thickness(14, 9, 14, 9)
            };

            button.SetResourceReference(FrameworkElement.StyleProperty, accent ? "AccentButton" : "DarkButtonStyle");
            return button;
        }

        private static Border CreateTitleBar(Window window, string title, bool showMaximizeButton)
        {
            var titleBarBorder = new Border { Height = 32 };
            titleBarBorder.SetResourceReference(Border.BackgroundProperty, "TitleBarBackground");

            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition());
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleBar.MouseDown += (_, e) =>
            {
                if (e.ChangedButton != MouseButton.Left)
                {
                    return;
                }

                if (showMaximizeButton && e.ClickCount == 2)
                {
                    ToggleWindowState(window);
                    return;
                }

                window.DragMove();
            };

            titleBar.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            });

            var controlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(controlsPanel, 1);

            controlsPanel.Children.Add(CreateWindowControlButton(window, "\u2500", "WindowControlButton", () =>
            {
                window.WindowState = WindowState.Minimized;
            }));

            if (showMaximizeButton)
            {
                var maximizeButton = CreateWindowControlButton(window, "\u25A1", "WindowControlButton", () =>
                {
                    ToggleWindowState(window);
                });

                void UpdateMaximizeButton()
                {
                    maximizeButton.Content = window.WindowState == WindowState.Maximized ? "\u2750" : "\u25A1";
                }

                UpdateMaximizeButton();
                window.StateChanged += (_, _) => UpdateMaximizeButton();
                controlsPanel.Children.Add(maximizeButton);
            }

            controlsPanel.Children.Add(CreateWindowControlButton(window, "\u2715", "CloseButtonStyle", window.Close));
            titleBar.Children.Add(controlsPanel);

            titleBarBorder.Child = titleBar;
            return titleBarBorder;
        }

        private static Button CreateWindowControlButton(Window window, string content, string styleKey, Action onClick)
        {
            var button = new Button
            {
                Content = content
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
            button.Click += (_, _) => onClick();
            return button;
        }

        private static void ToggleWindowState(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }
}
