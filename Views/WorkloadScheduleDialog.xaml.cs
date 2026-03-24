using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Contract2512.Models;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace Contract2512.Views
{
    public partial class WorkloadScheduleDialog : FluentWindow
    {
        private readonly Contract _contract;
        private readonly bool _isGroupMode;
        private readonly int _hoursPerWeek;
        private readonly int _weeksDuration;

        public Dictionary<string, List<TimeSlot>> Schedule { get; private set; }

        public WorkloadScheduleDialog(Contract contract, bool isGroupMode, int hoursPerWeek, int weeksDuration)
        {
            InitializeComponent();
            
            _contract = contract;
            _isGroupMode = isGroupMode;
            _hoursPerWeek = hoursPerWeek;
            _weeksDuration = weeksDuration;
            
            Schedule = new Dictionary<string, List<TimeSlot>>();
            
            LoadContractInfo();
        }

        private void LoadContractInfo()
        {
            string modeText = _isGroupMode ? "Групповой формат" : "Индивидуальный формат";
            string listenerName = _contract.Listener?.FullName ?? "Не указан";
            string programName = _contract.Program?.Name ?? "Не указана";
            
            ContractInfoTextBlock.Text = 
                $"Слушатель: {listenerName}\n" +
                $"Программа: {programName}\n" +
                $"Формат: {modeText}\n" +
                $"Учебная нагрузка: {_hoursPerWeek} акад. часов в неделю\n" +
                $"Продолжительность: {_weeksDuration} недель";
        }

        private void DayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                TextBox? timeTextBox = null;
                
                if (checkBox == MondayCheckBox) timeTextBox = MondayTimeTextBox;
                else if (checkBox == TuesdayCheckBox) timeTextBox = TuesdayTimeTextBox;
                else if (checkBox == WednesdayCheckBox) timeTextBox = WednesdayTimeTextBox;
                else if (checkBox == ThursdayCheckBox) timeTextBox = ThursdayTimeTextBox;
                else if (checkBox == FridayCheckBox) timeTextBox = FridayTimeTextBox;
                
                if (timeTextBox != null)
                {
                    timeTextBox.IsEnabled = checkBox.IsChecked == true;
                    if (checkBox.IsChecked == false)
                    {
                        timeTextBox.Text = string.Empty;
                    }
                }
            }
            
            ValidateSchedule();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateSchedule())
            {
                return;
            }
            
            // Собираем расписание
            Schedule.Clear();
            
            if (MondayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(MondayTimeTextBox.Text))
            {
                Schedule["Понедельник"] = ParseTimeSlots(MondayTimeTextBox.Text);
            }
            
            if (TuesdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(TuesdayTimeTextBox.Text))
            {
                Schedule["Вторник"] = ParseTimeSlots(TuesdayTimeTextBox.Text);
            }
            
            if (WednesdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(WednesdayTimeTextBox.Text))
            {
                Schedule["Среда"] = ParseTimeSlots(WednesdayTimeTextBox.Text);
            }
            
            if (ThursdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(ThursdayTimeTextBox.Text))
            {
                Schedule["Четверг"] = ParseTimeSlots(ThursdayTimeTextBox.Text);
            }
            
            if (FridayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(FridayTimeTextBox.Text))
            {
                Schedule["Пятница"] = ParseTimeSlots(FridayTimeTextBox.Text);
            }
            
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateSchedule()
        {
            ValidationTextBlock.Visibility = Visibility.Collapsed;
            
            // Проверяем что выбран хотя бы один день
            bool anyDaySelected = MondayCheckBox.IsChecked == true ||
                                 TuesdayCheckBox.IsChecked == true ||
                                 WednesdayCheckBox.IsChecked == true ||
                                 ThursdayCheckBox.IsChecked == true ||
                                 FridayCheckBox.IsChecked == true;
            
            if (!anyDaySelected)
            {
                ShowValidationError("Выберите хотя бы один день недели для занятий.");
                return false;
            }
            
            // Подсчитываем общее количество часов в неделю
            int totalHours = 0;
            
            if (MondayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(MondayTimeTextBox.Text))
            {
                var slots = ParseTimeSlots(MondayTimeTextBox.Text);
                if (slots == null)
                {
                    ShowValidationError("Неверный формат времени для понедельника. Используйте формат: 10:00-10:45, 11:00-11:45");
                    return false;
                }
                totalHours += slots.Count;
            }
            
            if (TuesdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(TuesdayTimeTextBox.Text))
            {
                var slots = ParseTimeSlots(TuesdayTimeTextBox.Text);
                if (slots == null)
                {
                    ShowValidationError("Неверный формат времени для вторника. Используйте формат: 10:00-10:45, 11:00-11:45");
                    return false;
                }
                totalHours += slots.Count;
            }
            
            if (WednesdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(WednesdayTimeTextBox.Text))
            {
                var slots = ParseTimeSlots(WednesdayTimeTextBox.Text);
                if (slots == null)
                {
                    ShowValidationError("Неверный формат времени для среды. Используйте формат: 10:00-10:45, 11:00-11:45");
                    return false;
                }
                totalHours += slots.Count;
            }
            
            if (ThursdayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(ThursdayTimeTextBox.Text))
            {
                var slots = ParseTimeSlots(ThursdayTimeTextBox.Text);
                if (slots == null)
                {
                    ShowValidationError("Неверный формат времени для четверга. Используйте формат: 10:00-10:45, 11:00-11:45");
                    return false;
                }
                totalHours += slots.Count;
            }
            
            if (FridayCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(FridayTimeTextBox.Text))
            {
                var slots = ParseTimeSlots(FridayTimeTextBox.Text);
                if (slots == null)
                {
                    ShowValidationError("Неверный формат времени для пятницы. Используйте формат: 10:00-10:45, 11:00-11:45");
                    return false;
                }
                totalHours += slots.Count;
            }
            
            // Для индивидуального формата проверяем соответствие часам в неделю
            if (!_isGroupMode && totalHours != _hoursPerWeek)
            {
                ShowValidationError($"Для индивидуального формата количество часов в неделю должно быть {_hoursPerWeek}. Сейчас указано: {totalHours}");
                return false;
            }
            
            // Для группового формата просто предупреждаем если не совпадает
            if (_isGroupMode && totalHours != _hoursPerWeek)
            {
                ShowValidationError($"Внимание: указано {totalHours} часов в неделю, а в договоре {_hoursPerWeek}. Для группового формата это допустимо, но рекомендуется соблюдать соответствие.");
                // Не возвращаем false, просто предупреждаем
            }
            
            return true;
        }

        private void ShowValidationError(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;
        }

        private List<TimeSlot>? ParseTimeSlots(string input)
        {
            try
            {
                var slots = new List<TimeSlot>();
                
                // Разделяем по запятой
                var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    
                    // Формат: 10:00-10:45
                    var match = Regex.Match(trimmed, @"(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})");
                    if (!match.Success)
                    {
                        return null;
                    }
                    
                    int startHour = int.Parse(match.Groups[1].Value);
                    int startMinute = int.Parse(match.Groups[2].Value);
                    int endHour = int.Parse(match.Groups[3].Value);
                    int endMinute = int.Parse(match.Groups[4].Value);
                    
                    slots.Add(new TimeSlot
                    {
                        StartTime = new TimeSpan(startHour, startMinute, 0),
                        EndTime = new TimeSpan(endHour, endMinute, 0)
                    });
                }
                
                return slots;
            }
            catch
            {
                return null;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }

    public class TimeSlot
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        public override string ToString()
        {
            return $"{StartTime:HH\\:mm}-{EndTime:HH\\:mm}";
        }
    }
}
