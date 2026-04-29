using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Contract2512.Models;
using Contract2512.Services;
using Microsoft.EntityFrameworkCore;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Contract2512.Views
{
    public sealed class OrderCreationControl : UserControl
    {
        private readonly OrderDocumentService _orderService = new();
        private readonly List<LearningProgram> _programs;
        private readonly List<Teacher> _teachers;
        private readonly DataGrid _documentsGrid;
        public event EventHandler? OrderCreated;

        public OrderCreationControl()
        {
            _programs = LoadPrograms();
            _teachers = LoadTeachers();
            _documentsGrid = CreateDocumentsGrid();
            Content = BuildLayout();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headerCard = CreateCard(
                "Приказы",
                CreateDescription(
                    "Генерация приказов и ведомостей по шаблонам из папки C:\\Dogovora\\Приказы. " +
                    "Шаблоны берутся из таблицы order_template, а готовые файлы сохраняются в историю."));
            Grid.SetRow(headerCard, 0);
            root.Children.Add(headerCard);

            var tabControl = new TabControl
            {
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(tabControl, 1);
            root.Children.Add(tabControl);

            tabControl.Items.Add(BuildAdmissionTab());
            tabControl.Items.Add(BuildAdmissionGroupTab());
            tabControl.Items.Add(BuildExpulsionTab());
            tabControl.Items.Add(BuildCommissionCompositionTab());
            tabControl.Items.Add(BuildFinalAttestationTab());
            tabControl.Items.Add(BuildCommissionMeetingProtocolTab());
            tabControl.Items.Add(BuildStatementTab());

            return root;
        }

        private TabItem BuildAdmissionTab()
        {
            var programComboBox = CreateProgramComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var orderDatePicker = CreateDatePicker();
            var numberTextBox = CreateTextBox();
            var organizerFioTextBox = CreateTextBox();
            var organizerPostTextBox = CreateTextBox();

            var content = CreateScrollableForm(
                CreateCard(
                    "О зачислении",
                    CreateDescription("Для индивидуального приказа выбери программу, слушателя и укажи номер приказа."),
                    CreateField("Программа", programComboBox),
                    CreateSelectionSection(
                        "Слушатель",
                        CreateSelectionButton("Выбрать слушателя", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, false, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата приказа", orderDatePicker),
                    CreateField("Номер приказа", numberTextBox),
                    CreateField("Ответственный (ФИО)", organizerFioTextBox),
                    CreateField("Ответственный (должность)", organizerPostTextBox),
                    CreateActionButton("Сформировать приказ", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (selectedContracts.Count != 1)
                        {
                            ShowWarning("Для приказа о зачислении нужно выбрать одного слушателя.");
                            return;
                        }

                        var contract = selectedContracts[0];
                        var listener = contract.Listener;
                        if (listener == null)
                        {
                            ShowWarning("Не удалось загрузить слушателя по выбранному договору.");
                            return;
                        }

                        try
                        {
                            var date = orderDatePicker.SelectedDate ?? DateTime.Today;
                            var applicationDate = ResolveApplicationDate(
                                selectedContracts,
                                OrderDocumentService.AdmissionKey);
                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.AdmissionKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.AdmissionKey),
                                ProgramId = program.Id,
                                ContractId = contract.Id,
                                ListenerId = listener.Id,
                                DocumentNumber = Normalize(numberTextBox.Text),
                                SubjectName = listener.LastName,
                                Placeholders = BuildAdmissionPlaceholders(
                                    contract,
                                    program,
                                    date,
                                    applicationDate,
                                    Normalize(numberTextBox.Text),
                                    Normalize(organizerFioTextBox.Text),
                                    Normalize(organizerPostTextBox.Text)),
                                ApplicationEntries = new List<ApplicationSaveRequest>
                                {
                                    new()
                                    {
                                        ApplicationTypeKey = OrderDocumentService.AdmissionKey,
                                        ContractId = contract.Id,
                                        ListenerId = listener.Id,
                                        ProgramId = program.Id,
                                        ApplicationDate = applicationDate
                                    }
                                },
                                Metadata = new
                                {
                                    type = OrderDocumentService.AdmissionKey,
                                    contractId = contract.Id,
                                    listenerId = listener.Id,
                                    applicationDate = applicationDate.ToString("yyyy-MM-dd")
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании приказа: {ex.Message}");
                        }
                    })));

            return CreateTab("О зачислении", content);
        }

        private TabItem BuildAdmissionGroupTab()
        {
            var programComboBox = CreateProgramComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var orderDatePicker = CreateDatePicker();
            var numberTextBox = CreateTextBox();
            var organizerFioTextBox = CreateTextBox();
            var organizerPostTextBox = CreateTextBox();

            var content = CreateScrollableForm(
                CreateCard(
                    "О зачислении группа",
                    CreateDescription("Групповой приказ разворачивает строки слушателей прямо в таблице шаблона."),
                    CreateField("Программа", programComboBox),
                    CreateSelectionSection(
                        "Слушатели группы",
                        CreateSelectionButton("Выбрать слушателей", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, true, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата приказа", orderDatePicker),
                    CreateField("Номер приказа", numberTextBox),
                    CreateField("Ответственный (ФИО)", organizerFioTextBox),
                    CreateField("Ответственный (должность)", organizerPostTextBox),
                    CreateActionButton("Сформировать приказ", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (selectedContracts.Count == 0)
                        {
                            ShowWarning("Выберите хотя бы одного слушателя.");
                            return;
                        }

                        try
                        {
                            var orderDate = orderDatePicker.SelectedDate ?? DateTime.Today;
                            var applicationDate = ResolveApplicationDate(
                                selectedContracts,
                                OrderDocumentService.AdmissionGroupKey);
                            var firstContract = selectedContracts[0];
                            var rows = selectedContracts
                                .Select((contract, index) => BuildAdmissionGroupRow(contract, index + 1))
                                .ToList();

                            var placeholders = BuildCommonOrderPlaceholders(orderDate);
                            placeholders["{{Num_CK}}"] = Normalize(numberTextBox.Text);
                            placeholders["{{Chislo_Zayavleniya}}"] = applicationDate.ToString("dd");
                            placeholders["{{mounth_Zayavleniya}}"] = GetRussianMonthName(applicationDate);
                            placeholders["{{Mounth_Zayavleniya}}"] = GetRussianMonthName(applicationDate);
                            placeholders["{{year_Zayavleniya}}"] = $"{applicationDate:yyyy} г.";
                            placeholders["{{Year_Zayavleniya}}"] = $"{applicationDate:yyyy} г.";
                            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
                            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
                            placeholders["{{time_program}}"] = $"{program.Hours} академических часов";
                            placeholders["{{Option_Time}}"] = GetOptionTimeText(firstContract);
                            placeholders["{{FIO_organizator}}"] = Normalize(organizerFioTextBox.Text);
                            placeholders["{{Post_organizator}}"] = Normalize(organizerPostTextBox.Text);

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.AdmissionGroupKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.AdmissionGroupKey),
                                ProgramId = program.Id,
                                DocumentNumber = Normalize(numberTextBox.Text),
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                TableRows = new List<TableExpansionRequest>
                                {
                                    new()
                                    {
                                        AnchorPlaceholder = "{{num}}",
                                        Rows = rows
                                    }
                                },
                                ApplicationEntries = selectedContracts
                                    .Where(contract => contract.Listener != null)
                                    .Select(contract => new ApplicationSaveRequest
                                    {
                                        ApplicationTypeKey = OrderDocumentService.AdmissionGroupKey,
                                        ContractId = contract.Id,
                                        ListenerId = contract.Listener!.Id,
                                        ProgramId = program.Id,
                                        ApplicationDate = applicationDate
                                    })
                                    .ToList(),
                                Metadata = new
                                {
                                    type = OrderDocumentService.AdmissionGroupKey,
                                    contractIds = selectedContracts.Select(c => c.Id).ToArray(),
                                    applicationDate = applicationDate.ToString("yyyy-MM-dd")
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании группового приказа: {ex.Message}");
                        }
                    })));

            return CreateTab("О зачислении группа", content);
        }

        private TabItem BuildExpulsionTab()
        {
            var programComboBox = CreateProgramComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var orderDatePicker = CreateDatePicker();
            var protocolNumberTextBox = CreateTextBox();

            var content = CreateScrollableForm(
                CreateCard(
                    "Об отчислении",
                    CreateDescription("Шаблон не содержит отдельного плейсхолдера для номера приказа, поэтому в него подставляются только даты и данные слушателей."),
                    CreateField("Программа", programComboBox),
                    CreateSelectionSection(
                        "Слушатели",
                        CreateSelectionButton("Выбрать слушателей", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, true, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата приказа", orderDatePicker),
                    CreateField("Номер протокола", protocolNumberTextBox),
                    CreateActionButton("Сформировать приказ", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (selectedContracts.Count == 0)
                        {
                            ShowWarning("Выберите хотя бы одного слушателя.");
                            return;
                        }

                        try
                        {
                            var date = orderDatePicker.SelectedDate ?? DateTime.Today;
                            var placeholders = BuildCommonOrderPlaceholders(date);
                            placeholders["{{num_prot}}"] = Normalize(protocolNumberTextBox.Text);
                            placeholders["{{Chislo_mounth}}"] = date.ToString("MM");
                            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
                            placeholders["{{Type_education}}"] = GetEducationSummaryForExpulsion(selectedContracts);
                            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
                            placeholders["{{FIO_Slushatel}}"] = BuildListenerListInDative(selectedContracts, "; ");

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.ExpulsionKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.ExpulsionKey),
                                ProgramId = program.Id,
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                Metadata = new
                                {
                                    type = OrderDocumentService.ExpulsionKey,
                                    contractIds = selectedContracts.Select(c => c.Id).ToArray()
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании приказа: {ex.Message}");
                        }
                    })));

            return CreateTab("Об отчислении", content);
        }

        private TabItem BuildCommissionCompositionTab()
        {
            var programComboBox = CreateProgramComboBox();
            var orderDatePicker = CreateDatePicker();
            var chairmanFioTextBox = CreateTextBox();
            var chairmanPostTextBox = CreateTextBox();
            var commissionMembers = new List<CommissionMemberInputRow>();
            var membersSection = CreateCommissionMembersSection(commissionMembers);
            var secretaryFioTextBox = CreateTextBox();
            var secretaryPostTextBox = CreateTextBox();

            var content = CreateScrollableForm(
                CreateCard(
                    "На состав комиссии",
                    CreateDescription("Заполняет состав аттестационной комиссии для выбранной программы."),
                    CreateField("Программа", programComboBox),
                    CreateField("Дата приказа", orderDatePicker),
                    CreateField("Председатель комиссии (ФИО)", chairmanFioTextBox),
                    CreateField("Председатель комиссии (должность)", chairmanPostTextBox),
                    membersSection,
                    CreateField("Секретарь комиссии (ФИО)", secretaryFioTextBox),
                    CreateField("Секретарь комиссии (должность)", secretaryPostTextBox),
                    CreateActionButton("Сформировать приказ", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        try
                        {
                            var date = orderDatePicker.SelectedDate ?? DateTime.Today;
                            var placeholders = BuildCommonOrderPlaceholders(date);
                            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
                            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
                            placeholders["{{FIO_Predsedatel}}"] = Normalize(chairmanFioTextBox.Text);
                            placeholders["{{Post_Predsedatel}}"] = Normalize(chairmanPostTextBox.Text);
                            var members = GetCommissionMembers(commissionMembers);
                            var formattedMembers = FormatCommissionMembers(members);
                            placeholders["{{FIO_commission}}; {{Post}}"] = formattedMembers;
                            placeholders["{{CommissionMembers}}"] = formattedMembers;
                            placeholders["{{FIO_secret}}"] = Normalize(secretaryFioTextBox.Text);
                            placeholders["{{Post_secret}}"] = Normalize(secretaryPostTextBox.Text);

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.CommissionCompositionKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.CommissionCompositionKey),
                                ProgramId = program.Id,
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                Metadata = new
                                {
                                    type = OrderDocumentService.CommissionCompositionKey
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании приказа: {ex.Message}");
                        }
                    })));

            return CreateTab("На состав комиссии", content);
        }

        private TabItem BuildFinalAttestationTab()
        {
            var programComboBox = CreateProgramComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var orderDatePicker = CreateDatePicker();

            var content = CreateScrollableForm(
                CreateCard(
                    "О допуске итоговой аттестации",
                    CreateDescription("Приказ допускает выбранных слушателей к итоговой аттестации по программе."),
                    CreateField("Программа", programComboBox),
                    CreateSelectionSection(
                        "Слушатели",
                        CreateSelectionButton("Выбрать слушателей", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, true, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата приказа", orderDatePicker),
                    CreateActionButton("Сформировать приказ", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (selectedContracts.Count == 0)
                        {
                            ShowWarning("Выберите хотя бы одного слушателя.");
                            return;
                        }

                        try
                        {
                            var date = orderDatePicker.SelectedDate ?? DateTime.Today;
                            var placeholders = BuildCommonOrderPlaceholders(date);
                            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
                            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
                            placeholders["{{FIO_Slushatel}}"] = BuildListenerList(selectedContracts, "; ");

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.FinalAttestationAdmissionKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.FinalAttestationAdmissionKey),
                                ProgramId = program.Id,
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                Metadata = new
                                {
                                    type = OrderDocumentService.FinalAttestationAdmissionKey,
                                    contractIds = selectedContracts.Select(c => c.Id).ToArray()
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании приказа: {ex.Message}");
                        }
                    })));

            return CreateTab("О допуске итоговой аттестации", content);
        }

        private TabItem BuildCommissionMeetingProtocolTab()
        {
            var programComboBox = CreateProgramComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var protocolDatePicker = CreateDatePicker();
            var protocolNumberTextBox = CreateTextBox();
            var qualificationTextBox = CreateTextBox();
            var gradeTextBox = CreateTextBox();
            var chairmanFioTextBox = CreateTextBox();
            var chairmanPostTextBox = CreateTextBox();
            var commissionMembers = new List<CommissionMemberInputRow>();
            var membersSection = CreateCommissionMembersSection(commissionMembers);
            var secretaryFioTextBox = CreateTextBox();
            var secretaryPostTextBox = CreateTextBox();

            var content = CreateScrollableForm(
                CreateCard(
                    "О заседании комиссии",
                    CreateDescription("Формирует протокол заседания комиссии с оценкой и квалификацией по выбранным слушателям."),
                    CreateField("Программа", programComboBox),
                    CreateSelectionSection(
                        "Слушатели",
                        CreateSelectionButton("Выбрать слушателей", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, true, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата протокола", protocolDatePicker),
                    CreateField("Номер протокола", protocolNumberTextBox),
                    CreateField("Квалификация", qualificationTextBox),
                    CreateField("Результат/оценка", gradeTextBox),
                    CreateField("Председатель комиссии (ФИО)", chairmanFioTextBox),
                    CreateField("Председатель комиссии (должность)", chairmanPostTextBox),
                    membersSection,
                    CreateField("Секретарь комиссии (ФИО)", secretaryFioTextBox),
                    CreateField("Секретарь комиссии (должность)", secretaryPostTextBox),
                    CreateActionButton("Сформировать протокол", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (selectedContracts.Count == 0)
                        {
                            ShowWarning("Выберите хотя бы одного слушателя.");
                            return;
                        }

                        try
                        {
                            var date = protocolDatePicker.SelectedDate ?? DateTime.Today;
                            var placeholders = BuildCommonOrderPlaceholders(date);
                            placeholders["{{num_protokol}}"] = Normalize(protocolNumberTextBox.Text);
                            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
                            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
                            AddDateRangePlaceholders(
                                placeholders,
                                GetStartDate(selectedContracts),
                                GetEndDate(selectedContracts));
                            placeholders["{{FIO_Predsedatel}}"] = Normalize(chairmanFioTextBox.Text);
                            placeholders["{{FIO_Predsedatel_Inicial}}"] = ToInitials(Normalize(chairmanFioTextBox.Text));
                            placeholders["{{Post_Predsedatel}}"] = Normalize(chairmanPostTextBox.Text);
                            var members = GetCommissionMembers(commissionMembers);
                            placeholders["{{FIO_commission}}; {{Post}}"] = FormatCommissionMembers(members);
                            placeholders["{{FIO_commission}}"] = JoinCommissionMembers(members, member => member.Fio);
                            placeholders["{{FIO_commission_Inicial}}"] = JoinCommissionMembers(members, member => ToInitials(member.Fio));
                            placeholders["{{Post}}"] = JoinCommissionMembers(members, member => member.Post);
                            placeholders["{{FIO_secret}}"] = Normalize(secretaryFioTextBox.Text);
                            placeholders["{{FIO_secret_Inicial}}"] = ToInitials(Normalize(secretaryFioTextBox.Text));
                            placeholders["{{Post_secret}}"] = Normalize(secretaryPostTextBox.Text);

                            var rowValues = selectedContracts.Select(contract => new Dictionary<string, string>
                            {
                                ["{{FIO_Slushatel}}"] = contract.Listener?.FullName ?? string.Empty,
                                ["{{Grade}}"] = Normalize(gradeTextBox.Text),
                                ["{{Kval}}"] = Normalize(qualificationTextBox.Text)
                            }).ToList();

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.CommissionMeetingProtocolKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.CommissionMeetingProtocolKey),
                                ProgramId = program.Id,
                                DocumentNumber = Normalize(protocolNumberTextBox.Text),
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                TableRows = new List<TableExpansionRequest>
                                {
                                    new()
                                    {
                                        AnchorPlaceholder = "{{FIO_Slushatel}}",
                                        Rows = rowValues
                                    }
                                },
                                Metadata = new
                                {
                                    type = OrderDocumentService.CommissionMeetingProtocolKey,
                                    contractIds = selectedContracts.Select(c => c.Id).ToArray()
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании протокола: {ex.Message}");
                        }
                    })));

            return CreateTab("О заседании комиссии", content);
        }

        private TabItem BuildStatementTab()
        {
            var programComboBox = CreateProgramComboBox();
            var moduleComboBox = CreateModuleComboBox();
            var teacherComboBox = CreateTeacherComboBox();
            var selectedContracts = new List<Contract>();
            var summaryText = CreateSummaryText();
            var statementDatePicker = CreateDatePicker();
            var statementNumberTextBox = CreateTextBox();
            var dateTimeTextBox = CreateTextBox("Например: 27.04.2026 10:00");

            programComboBox.SelectionChanged += (_, _) =>
            {
                if (programComboBox.SelectedItem is LearningProgram program)
                {
                    moduleComboBox.ItemsSource = LoadProgramModules(program.Id);
                    moduleComboBox.DisplayMemberPath = nameof(ProgramModule.ModuleName);
                    moduleComboBox.SelectedValuePath = nameof(ProgramModule.Id);
                    moduleComboBox.SelectedIndex = 0;
                }
                else
                {
                    moduleComboBox.ItemsSource = null;
                }
            };

            var content = CreateScrollableForm(
                CreateCard(
                    "Ведомости",
                    CreateDescription("Генерирует ведомость промежуточной аттестации по модулю и выбранным слушателям."),
                    CreateField("Программа", programComboBox),
                    CreateField("Модуль/дисциплина", moduleComboBox),
                    CreateField("Преподаватель", teacherComboBox),
                    CreateSelectionSection(
                        "Слушатели",
                        CreateSelectionButton("Выбрать слушателей", () =>
                        {
                            if (programComboBox.SelectedItem is not LearningProgram program)
                            {
                                ShowWarning("Сначала выберите программу.");
                                return;
                            }

                            var result = SelectContracts(program, true, selectedContracts);
                            if (result != null)
                            {
                                selectedContracts.Clear();
                                selectedContracts.AddRange(result);
                                UpdateSelectionSummary(summaryText, selectedContracts);
                            }
                        }),
                        summaryText),
                    CreateField("Дата ведомости", statementDatePicker),
                    CreateField("Номер ведомости", statementNumberTextBox),
                    CreateField("Дата и время", dateTimeTextBox),
                    CreateActionButton("Сформировать ведомость", () =>
                    {
                        if (programComboBox.SelectedItem is not LearningProgram program)
                        {
                            ShowWarning("Выберите программу.");
                            return;
                        }

                        if (moduleComboBox.SelectedItem is not ProgramModule module)
                        {
                            ShowWarning("Выберите модуль.");
                            return;
                        }

                        if (teacherComboBox.SelectedItem is not Teacher teacher)
                        {
                            ShowWarning("Выберите преподавателя.");
                            return;
                        }

                        if (selectedContracts.Count == 0)
                        {
                            ShowWarning("Выберите хотя бы одного слушателя.");
                            return;
                        }

                        try
                        {
                            var date = statementDatePicker.SelectedDate ?? DateTime.Today;
                            var placeholders = new Dictionary<string, string>
                            {
                                ["{{Date_Time}}"] = Normalize(dateTimeTextBox.Text),
                                ["{{year}}"] = date.ToString("yyyy"),
                                ["{{even_number}}"] = Normalize(statementNumberTextBox.Text),
                                ["{{Type_program}}"] = GetProgramTypeName(program),
                                ["{{Program_name}}"] = program.Name ?? string.Empty,
                                ["{{Module_name}}"] = module.ModuleName ?? string.Empty,
                                ["{{Teatcher}}"] = teacher.FullName ?? string.Empty
                            };

                            var rowValues = selectedContracts.Select((contract, index) => new Dictionary<string, string>
                            {
                                ["{{num}}"] = (index + 1).ToString(),
                                ["{{FIO_Slushatel}}"] = contract.Listener?.FullName ?? string.Empty
                            }).ToList();

                            var request = new OrderGenerationRequest
                            {
                                OrderTypeKey = OrderDocumentService.StatementKey,
                                OrderName = OrderDocumentService.GetOrderName(OrderDocumentService.StatementKey),
                                ProgramId = program.Id,
                                TeacherId = teacher.Id,
                                DocumentNumber = Normalize(statementNumberTextBox.Text),
                                SubjectName = program.Name,
                                Placeholders = placeholders,
                                TableRows = new List<TableExpansionRequest>
                                {
                                    new()
                                    {
                                        AnchorPlaceholder = "{{num}}",
                                        Rows = rowValues
                                    }
                                },
                                Metadata = new
                                {
                                    type = OrderDocumentService.StatementKey,
                                    contractIds = selectedContracts.Select(c => c.Id).ToArray(),
                                    moduleId = module.Id,
                                    teacherId = teacher.Id
                                }
                            };

                            GenerateAndOpen(request);
                        }
                        catch (Exception ex)
                        {
                            ShowError($"Ошибка при формировании ведомости: {ex.Message}");
                        }
                    })));

            return CreateTab("Ведомости", content);
        }

        private Border BuildHistoryCard()
        {
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            buttons.Children.Add(CreateSelectionButton("Открыть файл", OpenSelectedDocument));
            buttons.Children.Add(CreateSelectionButton("Обновить список", RefreshDocuments));

            return CreateCard(
                "История приказов",
                buttons,
                _documentsGrid);
        }

        private void GenerateAndOpen(OrderGenerationRequest request)
        {
            var path = _orderService.GenerateDocument(request);
            _orderService.OpenDocument(path);

            MessageBox.Show(
                $"Документ сформирован:\n{path}",
                "Успех",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            OrderCreated?.Invoke(this, EventArgs.Empty);
        }

        private List<LearningProgram> LoadPrograms()
        {
            using var db = new AppDbContext();
            return db.LearningPrograms
                .AsNoTracking()
                .Include(p => p.ProgramView)
                .OrderBy(p => p.Name)
                .ToList();
        }

        private List<Teacher> LoadTeachers()
        {
            using var db = new AppDbContext();
            return db.Teachers
                .AsNoTracking()
                .OrderBy(t => t.FullName)
                .ToList();
        }

        private List<ProgramModule> LoadProgramModules(long programId)
        {
            using var db = new AppDbContext();
            return db.ProgramModules
                .AsNoTracking()
                .Where(m => m.ProgramId == programId)
                .OrderBy(m => m.ModuleNumber)
                .ThenBy(m => m.ModuleName)
                .ToList();
        }

        private List<Contract> LoadContractsForProgram(long programId)
        {
            using var db = new AppDbContext();
            return db.Contracts
                .AsNoTracking()
                .Include(c => c.Listener)
                .Include(c => c.Program)
                    .ThenInclude(p => p!.ProgramView)
                .Include(c => c.ContractType)
                .Where(c => c.ProgramId == programId)
                .OrderByDescending(c => c.ContractDate)
                .ToList()
                .Where(c => c.Listener != null)
                .GroupBy(c => c.ListenerId)
                .Select(group => group.First())
                .OrderBy(c => c.Listener!.LastName)
                .ThenBy(c => c.Listener!.FirstName)
                .ToList();
        }

        private List<Contract>? SelectContracts(LearningProgram program, bool allowMultiple, List<Contract> currentSelection)
        {
            var availableContracts = LoadContractsForProgram(program.Id);
            if (availableContracts.Count == 0)
            {
                ShowWarning("Для выбранной программы не найдено договоров со слушателями.");
                return null;
            }

            var dialog = new ListenerSelectionDialog(availableContracts, currentSelection, allowMultiple)
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true
                ? dialog.SelectedContracts
                : null;
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

            if (string.IsNullOrWhiteSpace(document.FilePath) || !File.Exists(document.FilePath))
            {
                ShowWarning("Файл не найден. Возможно, он был удалён или перемещён.");
                return;
            }

            try
            {
                _orderService.OpenDocument(document.FilePath);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при открытии файла: {ex.Message}");
            }
        }

        private Dictionary<string, string> BuildAdmissionPlaceholders(
            Contract contract,
            LearningProgram program,
            DateTime orderDate,
            DateTime applicationDate,
            string documentNumber,
            string organizerFio,
            string organizerPost)
        {
            var listener = contract.Listener;
            var placeholders = BuildCommonOrderPlaceholders(orderDate);
            placeholders["{{Num_CK}}"] = documentNumber;
            placeholders["{{number_dogovor}}"] = contract.ContractNumber ?? string.Empty;
            placeholders["{{Date_dogovor}}"] = contract.ContractDate.ToString("dd.MM.yyyy");
            placeholders["{{Chislo_Zayavleniya}}"] = applicationDate.ToString("dd");
            placeholders["{{mounth_Zayavleniya}}"] = GetRussianMonthName(applicationDate);
            placeholders["{{Mounth_Zayavleniya}}"] = GetRussianMonthName(applicationDate);
            placeholders["{{year_Zayavleniya}}"] = $"{applicationDate:yyyy} г.";
            placeholders["{{Year_Zayavleniya}}"] = $"{applicationDate:yyyy} г.";
            placeholders["{{Type_program}}"] = GetProgramTypeName(program);
            placeholders["{{Program_name}}"] = program.Name ?? string.Empty;
            placeholders["{{time_program}}"] = $"{program.Hours} академических часов";
            placeholders["{{Education}}"] = listener == null ? string.Empty : GetEducationName(listener.Id);
            placeholders["{{Option_Time}}"] = GetOptionTimeText(contract);
            placeholders["{{FIO_organizator}}"] = organizerFio;
            placeholders["{{Post_organizator}}"] = organizerPost;
            placeholders["{{FIO_Slushatel}}"] = ConvertToGenitive(
                listener?.LastName ?? string.Empty,
                listener?.FirstName ?? string.Empty,
                listener?.Patronymic ?? string.Empty);
            return placeholders;
        }

        private Dictionary<string, string> BuildAdmissionGroupRow(Contract contract, int index)
        {
            return new Dictionary<string, string>
            {
                ["{{num}}"] = index.ToString(),
                ["{{FIO_Slushatel}}"] = contract.Listener?.FullName ?? string.Empty,
                ["{{Education}}"] = contract.Listener == null ? string.Empty : GetEducationName(contract.Listener.Id),
                ["{{DateStart}}"] = contract.StartDate?.ToString("dd.MM.yyyy") ?? string.Empty,
                ["{{Date Start}}"] = contract.StartDate?.ToString("dd.MM.yyyy") ?? string.Empty,
                ["{{DateEnd}}"] = contract.EndDate?.ToString("dd.MM.yyyy") ?? string.Empty,
                ["{{Date End}}"] = contract.EndDate?.ToString("dd.MM.yyyy") ?? string.Empty,
                ["{{number_dogovor}}"] = contract.ContractNumber ?? string.Empty,
                ["{{Date_dogovor}}"] = contract.ContractDate.ToString("dd.MM.yyyy")
            };
        }

        private static Dictionary<string, string> BuildCommonOrderPlaceholders(DateTime date)
        {
            return new Dictionary<string, string>
            {
                ["{{Chislo}}"] = date.ToString("dd"),
                ["{{mounth}}"] = GetRussianMonthName(date),
                ["{{year}}"] = date.ToString("yyyy")
            };
        }

        private static string GetRussianMonthName(DateTime date)
        {
            return date.Month switch
            {
                1 => "января",
                2 => "февраля",
                3 => "марта",
                4 => "апреля",
                5 => "мая",
                6 => "июня",
                7 => "июля",
                8 => "августа",
                9 => "сентября",
                10 => "октября",
                11 => "ноября",
                _ => "декабря"
            };
        }

        private static DateTime? GetStartDate(IEnumerable<Contract> contracts)
        {
            return contracts
                .Select(c => c.StartDate ?? c.ContractDate)
                .DefaultIfEmpty()
                .Min();
        }

        private static void AddDateRangePlaceholders(
            IDictionary<string, string> placeholders,
            DateTime? startDate,
            DateTime? endDate)
        {
            var startValue = startDate?.ToString("dd.MM.yyyy") ?? string.Empty;
            var endValue = endDate?.ToString("dd.MM.yyyy") ?? string.Empty;

            placeholders["{{DateStart}}"] = startValue;
            placeholders["{{Date Start}}"] = startValue;
            placeholders["{{DateEnd}}"] = endValue;
            placeholders["{{Date End}}"] = endValue;
        }

        private static DateTime? GetEndDate(IEnumerable<Contract> contracts)
        {
            return contracts
                .Select(c => c.EndDate ?? c.StartDate ?? c.ContractDate)
                .DefaultIfEmpty()
                .Max();
        }

        private string GetEducationName(long personId)
        {
            using var db = new AppDbContext();
            return db.Educations
                .AsNoTracking()
                .Where(e => e.PersonId == personId)
                .Select(e => e.BaseEducation != null ? e.BaseEducation.Name : string.Empty)
                .FirstOrDefault() ?? string.Empty;
        }

        private string GetEducationSummary(IEnumerable<Contract> contracts)
        {
            var values = contracts
                .Where(c => c.Listener != null)
                .Select(c => GetEducationName(c.Listener!.Id))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return values.Count == 0 ? string.Empty : string.Join(", ", values);
        }

        private string GetEducationSummaryForExpulsion(IEnumerable<Contract> contracts)
        {
            var values = contracts
                .Where(c => c.Listener != null)
                .Select(c => GetEducationName(c.Listener!.Id))
                .Select(ConvertEducationToGenitiveForm)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return values.Count == 0 ? string.Empty : string.Join(", ", values);
        }

        private static string ConvertEducationToGenitiveForm(string education)
        {
            if (string.IsNullOrWhiteSpace(education))
            {
                return string.Empty;
            }

            var normalized = education.Trim();

            if (normalized.Contains("среднее профессион", StringComparison.OrdinalIgnoreCase))
            {
                return "среднего профессионального";
            }

            if (normalized.Contains("высш", StringComparison.OrdinalIgnoreCase))
            {
                return "высшего";
            }

            if (normalized.Contains("средн", StringComparison.OrdinalIgnoreCase))
            {
                return "среднего";
            }

            return normalized.ToLowerInvariant();
        }

        private static string GetProgramTypeName(LearningProgram program)
        {
            return program.ProgramView?.Name ?? string.Empty;
        }

        private static string BuildListenerList(IEnumerable<Contract> contracts, string separator)
        {
            return string.Join(
                separator,
                contracts
                    .Select(c => c.Listener?.FullName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .Cast<string>());
        }

        private static string BuildListenerListInDative(IEnumerable<Contract> contracts, string separator)
        {
            return string.Join(
                separator,
                contracts
                    .Where(c => c.Listener != null)
                    .Select(c => ConvertToDative(
                        c.Listener!.LastName,
                        c.Listener.FirstName,
                        c.Listener.Patronymic ?? string.Empty))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct()
                    .Cast<string>());
        }

        private static string Normalize(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string NormalizeInlineText(string? value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static string ToInitials(string fullName)
        {
            var parts = fullName
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();

            if (parts.Length == 0)
            {
                return string.Empty;
            }

            if (parts.Length == 1)
            {
                return parts[0];
            }

            var result = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    result += $" {parts[i][0]}.";
                }
            }

            return result.Trim();
        }

        private static DateTime ResolveApplicationDate(
            IReadOnlyCollection<Contract> contracts,
            string preferredTypeKey)
        {
            if (contracts.Count == 0)
            {
                return DateTime.Today;
            }

            var contractIds = contracts
                .Select(c => c.Id)
                .Distinct()
                .ToList();

            using var db = new AppDbContext();
            var applications = db.ListenerApplications
                .AsNoTracking()
                .Where(a => contractIds.Contains(a.ContractId))
                .Select(a => new
                {
                    a.ContractId,
                    a.ApplicationTypeKey,
                    a.ApplicationDate,
                    a.UpdatedAt
                })
                .ToList();

            if (applications.Count > 0)
            {
                var preferred = applications
                    .Where(a => a.ApplicationTypeKey == preferredTypeKey)
                    .OrderByDescending(a => a.UpdatedAt)
                    .ToList();

                var fallback = applications
                    .Where(a => a.ApplicationTypeKey == OrderDocumentService.AdmissionKey)
                    .OrderByDescending(a => a.UpdatedAt)
                    .ToList();

                var selected = preferred.Count > 0 ? preferred : fallback.Count > 0 ? fallback : applications;
                var firstContract = contracts.FirstOrDefault();
                if (firstContract != null)
                {
                    var exactForFirst = selected.FirstOrDefault(a => a.ContractId == firstContract.Id);
                    if (exactForFirst != null)
                    {
                        return exactForFirst.ApplicationDate.Date;
                    }
                }

                return selected[0].ApplicationDate.Date;
            }

            return contracts
                .Select(c => c.ContractDate.Date)
                .FirstOrDefault(DateTime.Today);
        }

        private static string ConvertToGenitive(string lastName, string firstName, string patronymic)
        {
            var lastNameGen = ConvertLastNameToGenitive(lastName);
            var firstNameGen = ConvertFirstNameToGenitive(firstName);
            var patronymicGen = ConvertPatronymicToGenitive(patronymic);

            return $"{lastNameGen} {firstNameGen} {patronymicGen}".Trim();
        }

        private static string ConvertLastNameToGenitive(string lastName)
        {
            if (string.IsNullOrWhiteSpace(lastName))
            {
                return string.Empty;
            }

            lastName = lastName.Trim();

            if (lastName.EndsWith("ов") || lastName.EndsWith("ев") ||
                lastName.EndsWith("ин") || lastName.EndsWith("ын"))
            {
                return lastName + "а";
            }

            if (lastName.EndsWith("ский") || lastName.EndsWith("цкий"))
            {
                return lastName[..^2] + "ого";
            }

            if (lastName.EndsWith("ая"))
            {
                return lastName[..^2] + "ой";
            }

            if (lastName.EndsWith("яя"))
            {
                return lastName[..^2] + "ей";
            }

            if (!lastName.EndsWith("а") && !lastName.EndsWith("я"))
            {
                return lastName + "а";
            }

            if (lastName.EndsWith("а"))
            {
                return lastName[..^1] + "ы";
            }

            if (lastName.EndsWith("я"))
            {
                return lastName[..^1] + "и";
            }

            return lastName;
        }

        private static string ConvertFirstNameToGenitive(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
            {
                return string.Empty;
            }

            firstName = firstName.Trim();

            if (firstName.EndsWith("иль"))
            {
                return firstName[..^1] + "я";
            }

            if (firstName.EndsWith("а"))
            {
                return firstName[..^1] + "ы";
            }

            if (firstName.EndsWith("я"))
            {
                return firstName[..^1] + "и";
            }

            if (firstName.EndsWith("ия"))
            {
                return firstName[..^1] + "и";
            }

            if (firstName.EndsWith("й"))
            {
                return firstName[..^1] + "я";
            }

            return firstName + "а";
        }

        private static string ConvertPatronymicToGenitive(string patronymic)
        {
            if (string.IsNullOrWhiteSpace(patronymic))
            {
                return string.Empty;
            }

            patronymic = patronymic.Trim();

            if (patronymic.EndsWith("ович") || patronymic.EndsWith("евич"))
            {
                return patronymic + "а";
            }

            if (patronymic.EndsWith("ич"))
            {
                return patronymic + "а";
            }

            if (patronymic.EndsWith("овна") || patronymic.EndsWith("евна"))
            {
                return patronymic[..^1] + "ы";
            }

            if (patronymic.EndsWith("ична"))
            {
                return patronymic[..^1] + "ы";
            }

            return patronymic;
        }

        private static string ConvertToDative(string lastName, string firstName, string patronymic)
        {
            var lastNameDat = ConvertLastNameToDative(lastName);
            var firstNameDat = ConvertFirstNameToDative(firstName);
            var patronymicDat = ConvertPatronymicToDative(patronymic);

            return $"{lastNameDat} {firstNameDat} {patronymicDat}".Trim();
        }

        private static string ConvertLastNameToDative(string lastName)
        {
            if (string.IsNullOrWhiteSpace(lastName))
            {
                return string.Empty;
            }

            lastName = lastName.Trim();

            if (lastName.EndsWith("ов") || lastName.EndsWith("ев") ||
                lastName.EndsWith("ин") || lastName.EndsWith("ын"))
            {
                return lastName + "у";
            }

            if (lastName.EndsWith("ский") || lastName.EndsWith("цкий"))
            {
                return lastName[..^2] + "ому";
            }

            if (lastName.EndsWith("ая"))
            {
                return lastName[..^2] + "ой";
            }

            if (lastName.EndsWith("яя"))
            {
                return lastName[..^2] + "ей";
            }

            if (!lastName.EndsWith("а") && !lastName.EndsWith("я"))
            {
                return lastName + "у";
            }

            if (lastName.EndsWith("а") || lastName.EndsWith("я"))
            {
                return lastName[..^1] + "е";
            }

            return lastName;
        }

        private static string ConvertFirstNameToDative(string firstName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
            {
                return string.Empty;
            }

            firstName = firstName.Trim();

            if (firstName.EndsWith("ия"))
            {
                return firstName[..^1] + "и";
            }

            if (firstName.EndsWith("а") || firstName.EndsWith("я"))
            {
                return firstName[..^1] + "е";
            }

            if (firstName.EndsWith("й") || firstName.EndsWith("ь"))
            {
                return firstName[..^1] + "ю";
            }

            return firstName + "у";
        }

        private static string ConvertPatronymicToDative(string patronymic)
        {
            if (string.IsNullOrWhiteSpace(patronymic))
            {
                return string.Empty;
            }

            patronymic = patronymic.Trim();

            if (patronymic.EndsWith("ович") || patronymic.EndsWith("евич") || patronymic.EndsWith("ич"))
            {
                return patronymic + "у";
            }

            if (patronymic.EndsWith("овна") || patronymic.EndsWith("евна") || patronymic.EndsWith("ична"))
            {
                return patronymic[..^1] + "е";
            }

            return patronymic;
        }

        private string GetOptionTimeText(Contract contract)
        {
            if (!string.IsNullOrWhiteSpace(contract.StudyOptionKey) && IsDopContract(contract.ContractType?.Name))
            {
                var text = GetStudyOptionTexts().TryGetValue(contract.StudyOptionKey, out var studyText)
                    ? studyText
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            if (!string.IsNullOrWhiteSpace(contract.TimeOptionKey))
            {
                var timeOptions = IsPpContract(contract.ContractType?.Name)
                    ? GetPpTimeOptionTexts()
                    : IsDopContract(contract.ContractType?.Name)
                        ? GetDopTimeOptionTexts()
                        : GetPkTimeOptionTexts();

                if (timeOptions.TryGetValue(contract.TimeOptionKey, out var optionText))
                {
                    return optionText;
                }
            }

            return string.Empty;
        }

        private static bool IsDopContract(string? contractTypeName)
        {
            return !string.IsNullOrWhiteSpace(contractTypeName) &&
                   (contractTypeName.Contains("ДОП", StringComparison.OrdinalIgnoreCase) ||
                    contractTypeName.Contains("дополнительное", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPpContract(string? contractTypeName)
        {
            return !string.IsNullOrWhiteSpace(contractTypeName) &&
                   (contractTypeName.Contains("ПП", StringComparison.OrdinalIgnoreCase) ||
                    contractTypeName.Contains("проф", StringComparison.OrdinalIgnoreCase));
        }

        private static Dictionary<string, string> GetStudyOptionTexts() => new()
        {
            ["Option_study1"] = "Недельная учебная нагрузка по настоящему договору составляет 1 академический час в неделю; общая продолжительность освоения — 20 недель.",
            ["Option_study2"] = "Недельная учебная нагрузка по настоящему договору составляет 2 академических часа в неделю; общая продолжительность освоения — 10 недель.",
            ["Option_study3"] = "Недельная учебная нагрузка по настоящему договору составляет 4 академических часа в неделю; общая продолжительность освоения — 5 недель.",
            ["Option_study4"] = "Недельная учебная нагрузка по настоящему договору составляет 8 академических часов в неделю; общая продолжительность освоения — 2,5 недели.",
            ["Option_study5"] = "Недельная учебная нагрузка по настоящему договору составляет 10 академических часов в неделю; общая продолжительность освоения — 2 недели."
        };

        private static Dictionary<string, string> GetPkTimeOptionTexts() => new()
        {
            ["Option_Time1"] = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 21 неделя.",
            ["Option_Time2"] = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 11 недель.",
            ["Option_Time3"] = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 6 недель.",
            ["Option_Time4"] = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 5 недель.",
            ["Option_Time5"] = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели.",
            ["Option_Time6"] = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 3 недели."
        };

        private static Dictionary<string, string> GetPpTimeOptionTexts() => new()
        {
            ["Option_Time1"] = "Недельная учебная нагрузка по настоящему договору составляет 3 академических часа в неделю, включая 2 академических часа взаимодействия с преподавателем и 1 академический час самостоятельной работы; общая продолжительность освоения — 81 неделя.",
            ["Option_Time2"] = "Недельная учебная нагрузка по настоящему договору составляет 6 академических часов в неделю, включая 4 академических часа взаимодействия с преподавателем и 2 академических часа самостоятельной работы; общая продолжительность освоения — 41 неделя.",
            ["Option_Time3"] = "Недельная учебная нагрузка по настоящему договору составляет 12 академических часов в неделю, включая 8 академических часов взаимодействия с преподавателем и 4 академических часа самостоятельной работы; общая продолжительность освоения — 21 неделя.",
            ["Option_Time4"] = "Недельная учебная нагрузка по настоящему договору составляет 15 академических часов в неделю, включая 10 академических часов взаимодействия с преподавателем и 5 академических часов самостоятельной работы; общая продолжительность освоения — 17 недель.",
            ["Option_Time5"] = "Недельная учебная нагрузка по настоящему договору составляет 30 академических часов в неделю, включая 20 академических часов взаимодействия с преподавателем и 10 академических часов самостоятельной работы; общая продолжительность освоения — 9 недель.",
            ["Option_Time6"] = "Недельная учебная нагрузка по настоящему договору составляет 32 академических часа в неделю, включая 20 академических часов взаимодействия с преподавателем и 12 академических часов самостоятельной работы; общая продолжительность освоения — 8 недель."
        };

        private static Dictionary<string, string> GetDopTimeOptionTexts() => new()
        {
            ["Option_Time1"] = "Вариант 1 — с пониженной недельной учебной нагрузкой (1 акад. час в неделю).",
            ["Option_Time2"] = "Вариант 2 — с умеренной недельной учебной нагрузкой (2 акад. часа в неделю).",
            ["Option_Time3"] = "Вариант 3 — со стандартной недельной учебной нагрузкой (4 акад. часа в неделю).",
            ["Option_Time4"] = "Вариант 4 — с высокой недельной учебной нагрузкой (8 акад. часов в неделю).",
            ["Option_Time5"] = "Вариант 5 — с повышенной недельной учебной нагрузкой (10 акад. часов в неделю)."
        };

        private static TabItem CreateTab(string header, UIElement content)
        {
            return new TabItem
            {
                Header = header,
                Content = content
            };
        }

        private static ScrollViewer CreateScrollableForm(params UIElement[] children)
        {
            var panel = new StackPanel();
            foreach (var child in children)
            {
                panel.Children.Add(child);
            }

            return new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
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

        private static TextBlock CreateDescription(string text)
        {
            return new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                Margin = new Thickness(0, 0, 0, 16)
            };
        }

        private static StackPanel CreateField(string label, Control control)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(control);
            return panel;
        }

        private static StackPanel CreateSelectionSection(string label, Button button, TextBlock summaryText)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            panel.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(button);
            panel.Children.Add(summaryText);
            return panel;
        }

        private static TextBlock CreateSummaryText()
        {
            return new TextBlock
            {
                Text = "Ничего не выбрано",
                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
        }

        private static StackPanel CreateCommissionMembersSection(List<CommissionMemberInputRow> members)
        {
            var section = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 12)
            };

            section.Children.Add(new TextBlock
            {
                Text = "Члены комиссии",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            section.Children.Add(new TextBlock
            {
                Text = "Можно добавить несколько членов комиссии. Пустые строки не будут включены в документ.",
                Foreground = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var rowsPanel = new StackPanel();
            section.Children.Add(rowsPanel);

            void RefreshRows()
            {
                for (var index = 0; index < members.Count; index++)
                {
                    var row = members[index];
                    row.TitleTextBlock.Text = $"Член комиссии {index + 1}";
                    row.RemoveButton.Visibility = members.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            var addButton = CreateSelectionButton("Добавить члена комиссии", () =>
            {
                AddRow();
            });
            void AddRow()
            {
                CommissionMemberInputRow? row = null;
                row = CreateCommissionMemberRow(() =>
                {
                    if (row == null)
                    {
                        return;
                    }

                    rowsPanel.Children.Remove(row.Container);
                    members.Remove(row);
                    RefreshRows();
                });

                members.Add(row);
                rowsPanel.Children.Add(row.Container);
                RefreshRows();
            }

            addButton.Margin = new Thickness(0, 6, 0, 0);
            section.Children.Add(addButton);

            AddRow();

            return section;
        }

        private static CommissionMemberInputRow CreateCommissionMemberRow(Action onRemove)
        {
            var fioTextBox = CreateTextBox();
            var postTextBox = CreateTextBox();
            var titleTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var removeButton = DialogThemeHelper.CreateDialogButton("Убрать");
            removeButton.Padding = new Thickness(10, 6, 10, 6);
            removeButton.MinHeight = 34;
            removeButton.Click += (_, _) => onRemove();

            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(48, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var panel = new StackPanel();

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(titleTextBlock);
            Grid.SetColumn(removeButton, 1);
            headerGrid.Children.Add(removeButton);

            panel.Children.Add(headerGrid);
            panel.Children.Add(CreateField("ФИО", fioTextBox));
            panel.Children.Add(CreateField("Должность", postTextBox));

            container.Child = panel;
            return new CommissionMemberInputRow(fioTextBox, postTextBox, container, titleTextBlock, removeButton);
        }

        private static Border CreateCommissionMemberRowElement(
            CommissionMemberInputRow row,
            int number,
            bool canRemove,
            Action onRemove)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(48, 30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var panel = new StackPanel();

            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock
            {
                Text = $"Член комиссии {number}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var removeButton = DialogThemeHelper.CreateDialogButton("Убрать");
            removeButton.Padding = new Thickness(10, 6, 10, 6);
            removeButton.MinHeight = 34;
            removeButton.Visibility = canRemove ? Visibility.Visible : Visibility.Collapsed;
            removeButton.Click += (_, _) => onRemove();
            Grid.SetColumn(removeButton, 1);
            headerGrid.Children.Add(removeButton);

            panel.Children.Add(headerGrid);
            panel.Children.Add(CreateField("ФИО", row.FioTextBox));
            panel.Children.Add(CreateField("Должность", row.PostTextBox));

            container.Child = panel;
            return container;
        }

        private static List<CommissionMemberData> GetCommissionMembers(IEnumerable<CommissionMemberInputRow> rows)
        {
            var members = new List<CommissionMemberData>();

            foreach (var row in rows)
            {
                var fio = NormalizeInlineText(row.FioTextBox.Text);
                var post = NormalizeInlineText(row.PostTextBox.Text);

                if (string.IsNullOrWhiteSpace(fio) && string.IsNullOrWhiteSpace(post))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(fio) || string.IsNullOrWhiteSpace(post))
                {
                    throw new InvalidOperationException("Для каждого члена комиссии нужно заполнить и ФИО, и должность.");
                }

                members.Add(new CommissionMemberData(fio, post));
            }

            if (members.Count == 0)
            {
                throw new InvalidOperationException("Добавьте хотя бы одного члена комиссии.");
            }

            return members;
        }

        private static string FormatCommissionMembers(IEnumerable<CommissionMemberData> members)
        {
            return string.Join(
                Environment.NewLine,
                members.Select((member, index) => $"{index + 1}. {member.Fio}; {member.Post}"));
        }

        private static string JoinCommissionMembers(IEnumerable<CommissionMemberData> members, Func<CommissionMemberData, string> selector)
        {
            return string.Join(
                Environment.NewLine,
                members
                    .Select(selector)
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static void UpdateSelectionSummary(TextBlock textBlock, IReadOnlyCollection<Contract> contracts)
        {
            if (contracts.Count == 0)
            {
                textBlock.Text = "Ничего не выбрано";
                return;
            }

            textBlock.Text = contracts.Count == 1
                ? contracts.First().Listener?.FullName ?? "Слушатель"
                : $"{contracts.Count} слушателей: {string.Join(", ", contracts.Select(c => c.Listener?.LastName).Where(v => !string.IsNullOrWhiteSpace(v)).Take(6))}";
        }

        private static Button CreateActionButton(string text, Action onClick)
        {
            var button = CreateSelectionButton(text, onClick);
            button.Margin = new Thickness(0, 8, 0, 0);
            return button;
        }

        private static Button CreateSelectionButton(string text, Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(16, 10, 16, 10),
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            button.SetResourceReference(StyleProperty, "AccentButton");
            button.Click += (_, _) => onClick();
            return button;
        }

        private ComboBox CreateProgramComboBox()
        {
            var comboBox = CreateComboBox();
            comboBox.ItemsSource = _programs;
            comboBox.DisplayMemberPath = nameof(LearningProgram.DisplayName);
            comboBox.SelectedValuePath = nameof(LearningProgram.Id);
            return comboBox;
        }

        private ComboBox CreateTeacherComboBox()
        {
            var comboBox = CreateComboBox();
            comboBox.ItemsSource = _teachers;
            comboBox.DisplayMemberPath = nameof(Teacher.FullName);
            comboBox.SelectedValuePath = nameof(Teacher.Id);
            return comboBox;
        }

        private static ComboBox CreateModuleComboBox() => CreateComboBox();

        private static ComboBox CreateComboBox()
        {
            var comboBox = new ComboBox
            {
                MinHeight = 34
            };
            comboBox.SetResourceReference(StyleProperty, "DarkComboBoxStyle");
            return comboBox;
        }

        private static TextBox CreateTextBox(string? toolTip = null)
        {
            var textBox = new TextBox
            {
                MinHeight = 34
            };
            textBox.SetResourceReference(StyleProperty, "DarkTextBoxStyle");

            if (!string.IsNullOrWhiteSpace(toolTip))
            {
                textBox.ToolTip = toolTip;
            }

            return textBox;
        }

        private static DatePicker CreateDatePicker()
        {
            var datePicker = new DatePicker
            {
                SelectedDate = DateTime.Today,
                MinHeight = 34
            };
            datePicker.SetResourceReference(StyleProperty, "DarkDatePickerStyle");
            return datePicker;
        }

        private static DataGrid CreateDocumentsGrid()
        {
            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single
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

        private sealed record CommissionMemberData(string Fio, string Post);

        private sealed class CommissionMemberInputRow
        {
            public CommissionMemberInputRow(
                TextBox fioTextBox,
                TextBox postTextBox,
                Border container,
                TextBlock titleTextBlock,
                Button removeButton)
            {
                FioTextBox = fioTextBox;
                PostTextBox = postTextBox;
                Container = container;
                TitleTextBlock = titleTextBlock;
                RemoveButton = removeButton;
            }

            public TextBox FioTextBox { get; }

            public TextBox PostTextBox { get; }

            public Border Container { get; }

            public TextBlock TitleTextBlock { get; }

            public Button RemoveButton { get; }
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
