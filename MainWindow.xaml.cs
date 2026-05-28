using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Contract2512.Models;
using Contract2512.Services;
using Contract2512.Views;
using Wpf.Ui.Controls;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Contract2512
{
    public partial class MainWindow : FluentWindow
    {
        private enum MainSection
        {
            Persons,
            Contracts,
            Programs,
            AiCourseDrafts,
            ContractTypes,
            Organizations,
            Workload,
            Orders,
            Archive
        }

        private readonly record struct ViewSnapshot(int Count, long MaxId, DateTime? LastChangedAt, int Checksum = 0);

        private static readonly TimeSpan AutoRefreshInterval = TimeSpan.FromSeconds(30);

        private System.Collections.ObjectModel.ObservableCollection<Person>? _allPersons;
        private System.Collections.ObjectModel.ObservableCollection<Contract>? _allContracts;
        private System.Collections.ObjectModel.ObservableCollection<Person>? _archivedPersons;
        private System.Collections.ObjectModel.ObservableCollection<Contract>? _archivedContracts;
        private System.Windows.Threading.DispatcherTimer? _autoRefreshTimer;
        private bool _dbConfigMissingNotified;
        private bool _isAutoRefreshInProgress;
        private bool _personsLoaded;
        private bool _contractsLoaded;
        private bool _programsLoaded;
        private bool _contractTypesLoaded;
        private bool _organizationsLoaded;
        private bool _workloadLoaded;
        private bool _ordersLoaded;
        private bool _archiveLoaded;
        private ViewSnapshot? _personsSnapshot;
        private ViewSnapshot? _contractsSnapshot;
        private ViewSnapshot? _programsSnapshot;
        private ViewSnapshot? _organizationsSnapshot;
        private ViewSnapshot? _workloadSnapshot;

        public MainWindow()
        {
            InitializeComponent();
            SetupNavigation();
            SetupDataGridClips();
            LoadActiveSection(forceReload: true);
            SetupAutoRefresh();
        }

        private void SetupAutoRefresh()
        {
            _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            _autoRefreshTimer.Interval = AutoRefreshInterval;
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
            
            System.Diagnostics.Debug.WriteLine($"Автообновление данных запущено (каждые {AutoRefreshInterval.TotalSeconds:0} секунд)");
        }

        private async void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isAutoRefreshInProgress || !IsDbConfigured())
            {
                return;
            }

            try
            {
                _isAutoRefreshInProgress = true;
                _autoRefreshTimer?.Stop();
                await RefreshActiveSectionIfChangedAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка при автообновлении: {ex.Message}");
            }
            finally
            {
                _isAutoRefreshInProgress = false;
                _autoRefreshTimer?.Start();
            }
        }

        private void SetupDataGridClips()
        {
            // Устанавливаем Clip для DataGrid при загрузке
            if (PersonsDataGridBorder != null)
            {
                var rect = new System.Windows.Rect(0, 0, PersonsDataGridBorder.ActualWidth, PersonsDataGridBorder.ActualHeight);
                PersonsDataGridBorder.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
            
            if (ContractsDataGridBorder != null)
            {
                var rect = new System.Windows.Rect(0, 0, ContractsDataGridBorder.ActualWidth, ContractsDataGridBorder.ActualHeight);
                ContractsDataGridBorder.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void SetupNavigation()
        {
            // Устанавливаем начальное выделение для "Физические лица"
            BtnPersons.Tag = "Selected";
            UpdateMenuSelection(BtnPersons);
        }

        private void UpdateMenuSelection(System.Windows.Controls.Border selectedButton)
        {
            // Сбрасываем выделение всех кнопок
            BtnPersons.Tag = null;
            BtnContracts.Tag = null;
            BtnPrograms.Tag = null;
            BtnAiCourseDrafts.Tag = null;
            BtnContractTypes.Tag = null;
            BtnOrganizations.Tag = null;
            BtnWorkload.Tag = null;
            BtnOrders.Tag = null;
            BtnArchive.Tag = null;
            
            // Устанавливаем выделение выбранной кнопки
            if (selectedButton != null)
            {
                selectedButton.Tag = "Selected";
            }
        }

        private void BtnPersons_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnPersons);
            PersonsPanel.Visibility = Visibility.Visible;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Persons);
        }

        private void BtnContracts_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnContracts);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Visible;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Contracts);
        }

        private void BtnPrograms_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnPrograms);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Visible;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Programs);
        }

        private void BtnAiCourseDrafts_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnAiCourseDrafts);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Visible;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureAiCourseDraftControlLoaded();

            if (AiCourseDraftHost.Content is AiCourseDraftControl aiCourseDraftControl)
            {
                aiCourseDraftControl.NotifyPanelShown();
            }
        }

        private void BtnContractTypes_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnContractTypes);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Visible;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.ContractTypes);
        }

        private void BtnOrganizations_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnOrganizations);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Visible;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Organizations);
        }

        private void BtnWorkload_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnWorkload);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Visible;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Workload);
        }

        private void BtnOrders_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnOrders);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Visible;
            ArchivePanel.Visibility = Visibility.Collapsed;
            EnsureSectionLoaded(MainSection.Orders);

            if (OrdersHost.Content is OrdersControl ordersControl)
            {
                ordersControl.NotifyPanelShown();
            }
        }

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            UpdateMenuSelection(BtnArchive);
            PersonsPanel.Visibility = Visibility.Collapsed;
            ContractsPanel.Visibility = Visibility.Collapsed;
            ProgramsPanel.Visibility = Visibility.Collapsed;
            AiCourseDraftsPanel.Visibility = Visibility.Collapsed;
            ContractTypesPanel.Visibility = Visibility.Collapsed;
            OrganizationsPanel.Visibility = Visibility.Collapsed;
            WorkloadPanel.Visibility = Visibility.Collapsed;
            OrdersPanel.Visibility = Visibility.Collapsed;
            ArchivePanel.Visibility = Visibility.Visible;
            EnsureSectionLoaded(MainSection.Archive);
        }

        private void BtnSupport_Click(object sender, RoutedEventArgs e)
        {
            // Не обновляем выделение меню, так как это не вкладка, а окно
            var window = new SupportWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Не обновляем выделение меню, так как это не вкладка, а окно
            try
            {
                var window = new DatabaseSettingsWindow();
                window.Owner = this;

                var result = window.ShowDialog();
                if (result == true)
                {
                    try
                    {
                        _dbConfigMissingNotified = false;
                        ResetLoadedState();
                        LoadActiveSection(forceReload: true);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка загрузки данных после подключения к БД:\n{ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Не удалось открыть окно настроек БД:\n{ex}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private bool IsDbConfigured()
        {
            var cs = EnvConfigService.Get(EnvConfigService.ConnectionStringKey);
            return !string.IsNullOrWhiteSpace(cs);
        }

        private void NotifyDbConfigMissingOnce()
        {
            if (_dbConfigMissingNotified)
                return;

            _dbConfigMissingNotified = true;
            System.Windows.MessageBox.Show(
                "Не задана строка подключения к базе данных.\nОткройте Настройки и укажите строку подключения.",
                "Подключение к БД",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void PersonsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void ContractsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void ContractTypesDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeIcon.Text = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                MaximizeIcon.Text = "❐";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void LoadData()
        {
            LoadActiveSection(forceReload: true);
        }

        private void ResetLoadedState()
        {
            _personsLoaded = false;
            _contractsLoaded = false;
            _programsLoaded = false;
            _contractTypesLoaded = false;
            _organizationsLoaded = false;
            _workloadLoaded = false;
            _ordersLoaded = false;
            _personsSnapshot = null;
            _contractsSnapshot = null;
            _programsSnapshot = null;
            _organizationsSnapshot = null;
            _workloadSnapshot = null;
        }

        private MainSection GetActiveSection()
        {
            if (ContractsPanel.Visibility == Visibility.Visible)
                return MainSection.Contracts;
            if (ProgramsPanel.Visibility == Visibility.Visible)
                return MainSection.Programs;
            if (AiCourseDraftsPanel.Visibility == Visibility.Visible)
                return MainSection.AiCourseDrafts;
            if (ContractTypesPanel.Visibility == Visibility.Visible)
                return MainSection.ContractTypes;
            if (OrganizationsPanel.Visibility == Visibility.Visible)
                return MainSection.Organizations;
            if (WorkloadPanel.Visibility == Visibility.Visible)
                return MainSection.Workload;
            if (OrdersPanel.Visibility == Visibility.Visible)
                return MainSection.Orders;
            if (ArchivePanel.Visibility == Visibility.Visible)
                return MainSection.Archive;
            return MainSection.Persons;
        }

        private void EnsureSectionLoaded(MainSection section)
        {
            if (IsSectionLoaded(section))
            {
                return;
            }

            LoadSection(section, forceReload: true);
        }

        private bool IsSectionLoaded(MainSection section)
        {
            return section switch
            {
                MainSection.Persons => _personsLoaded,
                MainSection.Contracts => _contractsLoaded,
                MainSection.Programs => _programsLoaded,
                MainSection.ContractTypes => _contractTypesLoaded,
                MainSection.Organizations => _organizationsLoaded,
                MainSection.Workload => _workloadLoaded,
                MainSection.Orders => _ordersLoaded,
                MainSection.Archive => _archiveLoaded,
                _ => true,
            };
        }

        private void LoadActiveSection(bool forceReload)
        {
            if (!IsDbConfigured())
            {
                NotifyDbConfigMissingOnce();
                return;
            }

            LoadSection(GetActiveSection(), forceReload);
        }

        private void LoadSection(MainSection section, bool forceReload)
        {
            if (!forceReload && IsSectionLoaded(section))
            {
                return;
            }

            switch (section)
            {
                case MainSection.Persons:
                    LoadPersons();
                    break;
                case MainSection.Contracts:
                    LoadContracts();
                    break;
                case MainSection.Programs:
                    LoadPrograms();
                    break;
                case MainSection.ContractTypes:
                    LoadContractTypes();
                    break;
                case MainSection.Organizations:
                    LoadOrganizations();
                    break;
                case MainSection.Workload:
                    LoadWorkloadDocuments();
                    break;
                case MainSection.Orders:
                    EnsureOrdersControlLoaded();
                    _ordersLoaded = true;
                    break;
                case MainSection.Archive:
                    LoadArchive();
                    break;
            }
        }

        private async Task RefreshActiveSectionIfChangedAsync()
        {
            switch (GetActiveSection())
            {
                case MainSection.Persons when _personsLoaded:
                    await RefreshPersonsIfChangedAsync();
                    break;
                case MainSection.Contracts when _contractsLoaded:
                    await RefreshContractsIfChangedAsync();
                    break;
                case MainSection.Programs when _programsLoaded:
                    await RefreshProgramsIfChangedAsync();
                    break;
                case MainSection.Organizations when _organizationsLoaded:
                    await RefreshOrganizationsIfChangedAsync();
                    break;
                case MainSection.Workload when _workloadLoaded:
                    await RefreshWorkloadIfChangedAsync();
                    break;
                case MainSection.Archive when _archiveLoaded:
                    await Dispatcher.InvokeAsync(() => LoadArchive(showErrors: false));
                    break;
            }
        }

        private async Task RefreshPersonsIfChangedAsync()
        {
            var snapshot = await Task.Run(GetPersonsSnapshot);
            if (_personsSnapshot == snapshot)
            {
                return;
            }

            var persons = await Task.Run(FetchPersons);
            await Dispatcher.InvokeAsync(() => BindPersons(persons, snapshot));
        }

        private async Task RefreshContractsIfChangedAsync()
        {
            var snapshot = await Task.Run(GetContractsSnapshot);
            if (_contractsSnapshot == snapshot)
            {
                return;
            }

            var contracts = await Task.Run(FetchContracts);
            await Dispatcher.InvokeAsync(() => BindContracts(contracts, snapshot));
        }

        private async Task RefreshProgramsIfChangedAsync()
        {
            var snapshot = await Task.Run(GetProgramsSnapshot);
            if (_programsSnapshot == snapshot)
            {
                return;
            }

            var programs = await Task.Run(FetchProgramViewModels);
            await Dispatcher.InvokeAsync(() => BindPrograms(programs, snapshot));
        }

        private async Task RefreshOrganizationsIfChangedAsync()
        {
            var snapshot = await Task.Run(GetOrganizationsSnapshot);
            if (_organizationsSnapshot == snapshot)
            {
                return;
            }

            var organizations = await Task.Run(FetchOrganizations);
            await Dispatcher.InvokeAsync(() => BindOrganizations(organizations, snapshot));
        }

        private async Task RefreshWorkloadIfChangedAsync()
        {
            var snapshot = await Task.Run(GetWorkloadSnapshot);
            if (_workloadSnapshot == snapshot)
            {
                return;
            }

            var workloadDocuments = await Task.Run(FetchWorkloadDocuments);
            await Dispatcher.InvokeAsync(() => BindWorkloadDocuments(workloadDocuments, snapshot));
        }

        private static void HandleLoadError(string title, Exception ex, bool showErrors)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(
                    $"{title}: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"{title}: {ex.Message}");
        }

        private static List<Person> FetchPersons()
        {
            using var db = new AppDbContext();
            return db.Persons
                .AsNoTracking()
                .Where(p => !p.IsArchived)
                .Include(p => p.Gender)
                .Include(p => p.Contacts)
                .ToList();
        }

        private static List<Contract> FetchContracts()
        {
            using var db = new AppDbContext();
            return db.Contracts
                .AsNoTracking()
                .Where(c => !c.IsArchived)
                .Include(c => c.ContractType)
                .Include(c => c.Program)
                .Include(c => c.Payer)
                    .ThenInclude(p => p.Contacts)
                .Include(c => c.Listener)
                    .ThenInclude(p => p.Contacts)
                .ToList();
        }

        private List<ProgramViewModel> FetchProgramViewModels()
        {
            using var db = new AppDbContext();
            var programViewNames = db.ProgramViews
                .AsNoTracking()
                .ToDictionary(pv => pv.Id, pv => pv.Name ?? string.Empty);

            return db.LearningPrograms
                .AsNoTracking()
                .ToList()
                .Select(p => new ProgramViewModel
                {
                    Id = p.Id,
                    Name = p.Name ?? string.Empty,
                    Format = p.Format ?? string.Empty,
                    Hours = p.Hours,
                    LessonsCount = p.LessonsCount,
                    Price = p.Price,
                    ImagePath = GetImagePath(p.Image),
                    ProgramViewName = programViewNames.TryGetValue(p.ProgramViewId, out var name) ? name : string.Empty
                })
                .ToList();
        }

        private static List<WorkloadDocument> FetchWorkloadDocuments()
        {
            using var db = new AppDbContext();
            return db.WorkloadDocuments
                .AsNoTracking()
                .Include(w => w.Program)
                .Include(w => w.Listener)
                .Include(w => w.Teacher)
                .OrderByDescending(w => w.GeneratedAt)
                .ToList();
        }

        private static List<Organization> FetchOrganizations()
        {
            using var db = new AppDbContext();
            return db.Organizations
                .AsNoTracking()
                .ToList();
        }

        private void BindPersons(List<Person> persons, ViewSnapshot snapshot)
        {
            _allPersons = new System.Collections.ObjectModel.ObservableCollection<Person>(persons);
            ApplyPersonFilter();
            _personsSnapshot = snapshot;
            _personsLoaded = true;
        }

        private void BindContracts(List<Contract> contracts, ViewSnapshot snapshot)
        {
            _allContracts = new System.Collections.ObjectModel.ObservableCollection<Contract>(contracts);
            ApplyContractFilter();
            _contractsSnapshot = snapshot;
            _contractsLoaded = true;
        }

        private void BindPrograms(List<ProgramViewModel> programViewModels, ViewSnapshot snapshot)
        {
            ProgramsItemsControl.ItemsSource = programViewModels;
            _programsSnapshot = snapshot;
            _programsLoaded = true;
        }

        private void BindOrganizations(List<Organization> organizations, ViewSnapshot snapshot)
        {
            OrganizationsDataGrid.ItemsSource = organizations;
            _organizationsSnapshot = snapshot;
            _organizationsLoaded = true;
        }

        private void BindWorkloadDocuments(List<WorkloadDocument> workloadDocuments, ViewSnapshot snapshot)
        {
            WorkloadDocumentsDataGrid.ItemsSource = workloadDocuments;
            _workloadSnapshot = snapshot;
            _workloadLoaded = true;
        }

        private static ViewSnapshot GetPersonsSnapshot()
        {
            using var db = new AppDbContext();
            var persons = db.Persons.AsNoTracking().Where(p => !p.IsArchived);
            var count = persons.Count();
            if (count == 0)
            {
                return default;
            }

            return new ViewSnapshot(
                count,
                persons.Max(p => p.Id),
                persons.Max(p => p.UpdatedAt ?? p.CreatedAt));
        }

        private static ViewSnapshot GetContractsSnapshot()
        {
            using var db = new AppDbContext();
            var rows = db.Contracts
                .AsNoTracking()
                .Where(c => !c.IsArchived)
                .Select(c => new
                {
                    c.Id,
                    c.ContractNumber,
                    c.ContractDate,
                    c.ContractTypeId,
                    c.ProgramId,
                    c.StartDate,
                    c.EndDate,
                    c.IsGroup,
                    c.PayerId,
                    c.ListenerId,
                    c.SignerId,
                    c.ItogDocumentOptionKey,
                    c.TimeOptionKey,
                    c.StudyOptionKey,
                    c.PaymentOptionKey,
                    c.IsArchived,
                    c.ArchivedAt,
                    c.CreatedAt
                })
                .OrderBy(c => c.Id)
                .ToList();

            if (rows.Count == 0)
            {
                return default;
            }

            var checksum = new HashCode();
            foreach (var row in rows)
            {
                checksum.Add(row.Id);
                checksum.Add(row.ContractNumber);
                checksum.Add(row.ContractDate);
                checksum.Add(row.ContractTypeId);
                checksum.Add(row.ProgramId);
                checksum.Add(row.StartDate);
                checksum.Add(row.EndDate);
                checksum.Add(row.IsGroup);
                checksum.Add(row.PayerId);
                checksum.Add(row.ListenerId);
                checksum.Add(row.SignerId);
                checksum.Add(row.ItogDocumentOptionKey);
                checksum.Add(row.TimeOptionKey);
                checksum.Add(row.StudyOptionKey);
                checksum.Add(row.PaymentOptionKey);
                checksum.Add(row.IsArchived);
                checksum.Add(row.ArchivedAt);
            }

            return new ViewSnapshot(
                rows.Count,
                rows[^1].Id,
                rows.Max(c => c.CreatedAt),
                checksum.ToHashCode());
        }

        private static ViewSnapshot GetProgramsSnapshot()
        {
            using var db = new AppDbContext();
            var rows = db.LearningPrograms
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Format,
                    p.ProgramViewId,
                    p.Hours,
                    p.LessonsCount,
                    p.Price,
                    p.Image,
                    p.SourceUrl,
                    p.CreatedAt
                })
                .OrderBy(p => p.Id)
                .ToList();

            if (rows.Count == 0)
            {
                return default;
            }

            var checksum = new HashCode();
            foreach (var row in rows)
            {
                checksum.Add(row.Id);
                checksum.Add(row.Name);
                checksum.Add(row.Format);
                checksum.Add(row.ProgramViewId);
                checksum.Add(row.Hours);
                checksum.Add(row.LessonsCount);
                checksum.Add(row.Price);
                checksum.Add(row.Image);
                checksum.Add(row.SourceUrl);
            }

            return new ViewSnapshot(
                rows.Count,
                rows[^1].Id,
                rows.Max(p => p.CreatedAt),
                checksum.ToHashCode());
        }

        private static ViewSnapshot GetOrganizationsSnapshot()
        {
            using var db = new AppDbContext();
            var organizations = db.Organizations.AsNoTracking();
            var count = organizations.Count();
            if (count == 0)
            {
                return default;
            }

            return new ViewSnapshot(
                count,
                organizations.Max(o => o.Id),
                organizations.Max(o => o.UpdatedAt ?? o.CreatedAt));
        }

        private static ViewSnapshot GetWorkloadSnapshot()
        {
            using var db = new AppDbContext();
            var workloadDocuments = db.WorkloadDocuments.AsNoTracking();
            var count = workloadDocuments.Count();
            if (count == 0)
            {
                return default;
            }

            return new ViewSnapshot(
                count,
                workloadDocuments.Max(w => w.Id),
                workloadDocuments.Max(w => w.GeneratedAt));
        }

        private void LoadPersons(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                BindPersons(FetchPersons(), GetPersonsSnapshot());
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке данных", ex, showErrors);
            }
        }

        private void LoadContracts(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                BindContracts(FetchContracts(), GetContractsSnapshot());
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке договоров", ex, showErrors);
            }
        }

        private void LoadArchive(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                using var db = new AppDbContext();

                _archivedPersons = new System.Collections.ObjectModel.ObservableCollection<Person>(db.Persons
                    .AsNoTracking()
                    .Where(p => p.IsArchived)
                    .Include(p => p.Contacts)
                    .OrderByDescending(p => p.ArchivedAt)
                    .ThenBy(p => p.LastName)
                    .ToList());

                _archivedContracts = new System.Collections.ObjectModel.ObservableCollection<Contract>(db.Contracts
                    .AsNoTracking()
                    .Where(c => c.IsArchived)
                    .Include(c => c.ContractType)
                    .Include(c => c.Program)
                    .Include(c => c.Payer)
                    .Include(c => c.Listener)
                    .OrderByDescending(c => c.ArchivedAt)
                    .ThenByDescending(c => c.ContractDate)
                    .ToList());

                ApplyArchivePersonFilter();
                ApplyArchiveContractFilter();

                _archiveLoaded = true;
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке архива", ex, showErrors);
            }
        }

        private void ApplyPersonFilter()
        {
            if (_allPersons == null)
                return;

            string searchText = PersonSearchTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                PersonsDataGrid.ItemsSource = _allPersons;
                return;
            }

            PersonsDataGrid.ItemsSource = _allPersons
                .Where(p => p.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void ApplyContractFilter()
        {
            if (_allContracts == null)
                return;

            string searchText = ContractSearchTextBox?.Text?.Trim() ?? "";
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ContractsDataGrid.ItemsSource = _allContracts;
            }
            else
            {
                var filtered = _allContracts
                    .Where(c => c.ContractNumber != null && 
                               c.ContractNumber.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                ContractsDataGrid.ItemsSource = filtered;
            }
        }

        private void ContractSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyContractFilter();
        }

        private void ApplyArchivePersonFilter()
        {
            if (_archivedPersons == null)
                return;

            string searchText = ArchivedPersonSearchTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ArchivedPersonsDataGrid.ItemsSource = _archivedPersons;
                return;
            }

            ArchivedPersonsDataGrid.ItemsSource = _archivedPersons
                .Where(p => p.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void ApplyArchiveContractFilter()
        {
            if (_archivedContracts == null)
                return;

            string searchText = ArchivedContractSearchTextBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ArchivedContractsDataGrid.ItemsSource = _archivedContracts;
                return;
            }

            ArchivedContractsDataGrid.ItemsSource = _archivedContracts
                .Where(c => c.ContractNumber != null &&
                            c.ContractNumber.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        private void PersonSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyPersonFilter();
        }

        private void ArchivedPersonSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyArchivePersonFilter();
        }

        private void ArchivedContractSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyArchiveContractFilter();
        }

        private void ArchivePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is not Person selectedPerson)
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для переноса в архив!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Перенести {selectedPerson.FullName} в архив?",
                "Подтверждение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                using var db = new AppDbContext();
                var person = db.Persons.Find(selectedPerson.Id);
                if (person == null)
                    return;

                person.IsArchived = true;
                person.ArchivedAt = DateTime.Now;
                person.UpdatedAt = DateTime.Now;
                db.SaveChanges();

                _personsLoaded = false;
                _archiveLoaded = false;
                LoadPersons();
                LoadArchive(showErrors: false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при переносе в архив: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void ArchiveContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractsDataGrid.SelectedItem is not Contract selectedContract)
            {
                System.Windows.MessageBox.Show(
                    "Выберите договор для переноса в архив!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Перенести договор {selectedContract.ContractNumber} в архив?",
                "Подтверждение",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                using var db = new AppDbContext();
                var contract = db.Contracts.Find(selectedContract.Id);
                if (contract == null)
                    return;

                contract.IsArchived = true;
                contract.ArchivedAt = DateTime.Now;
                db.SaveChanges();

                _contractsLoaded = false;
                _archiveLoaded = false;
                LoadContracts();
                LoadArchive(showErrors: false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при переносе договора в архив: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            }
        }

        private void EditTimeOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new TimeOptionsWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void EditStudyOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new StudyOptionsWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void RestoreArchivedPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivedPersonsDataGrid.SelectedItem is not Person selectedPerson)
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо в архиве!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var person = db.Persons.Find(selectedPerson.Id);
                if (person == null)
                    return;

                person.IsArchived = false;
                person.ArchivedAt = null;
                person.UpdatedAt = DateTime.Now;
                db.SaveChanges();

                _personsLoaded = false;
                LoadPersons();
                LoadArchive();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при возврате из архива: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void RestoreArchivedContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivedContractsDataGrid.SelectedItem is not Contract selectedContract)
            {
                System.Windows.MessageBox.Show(
                    "Выберите договор в архиве!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                var contract = db.Contracts.Find(selectedContract.Id);
                if (contract == null)
                    return;

                contract.IsArchived = false;
                contract.ArchivedAt = null;
                db.SaveChanges();

                _contractsLoaded = false;
                LoadContracts();
                LoadArchive();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при возврате договора из архива: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void RefreshArchiveButton_Click(object sender, RoutedEventArgs e)
        {
            LoadArchive();
        }

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new PersonWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadPersons();
            }
        }

        private void EditPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonWindow(selectedPerson);
                ((Window)window).Owner = this;
                if (((Window)window).ShowDialog() == true)
                {
                    LoadPersons();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeletePersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить {selectedPerson.FullName}?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var person = db.Persons.Find(selectedPerson.Id);
                            if (person != null)
                            {
                                db.Persons.Remove(person);
                                db.SaveChanges();
                                LoadPersons();
                                System.Windows.MessageBox.Show(
                                    "Физическое лицо успешно удалено!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void CreateContractForPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new ContractWindow(selectedPerson, selectedPerson);
                ((Window)window).Owner = this;
                if (((Window)window).ShowDialog() == true)
                {
                    LoadContracts();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void CreateContractButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ContractWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadContracts();
            }
        }

        private void OpenContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractsDataGrid.SelectedItem is Contract selectedContract)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var contract = db.Contracts.FirstOrDefault(c => c.Id == selectedContract.Id);
                        if (contract != null)
                        {
                            // Получаем путь к сохраненному договору
                            string documentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Документы", "Договоры");
                            string fileName = $"Договор_{contract.ContractNumber}_{contract.Listener?.LastName}_{contract.ContractDate:yyyyMMdd}.docx";
                            string documentPath = Path.Combine(documentsFolder, fileName);

                            if (File.Exists(documentPath))
                            {
                                // Открываем сохраненный договор
                                var wordService = new WordDocumentService();
                                wordService.OpenDocument(documentPath);
                            }
                            else
                            {
                                System.Windows.MessageBox.Show(
                                    "Файл договора не найден! Возможно, он был удален или перемещен.",
                                    "Ошибка",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(
                                "Договор не найден!",
                                "Ошибка",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Ошибка при открытии договора: {ex.Message}",
                        "Ошибка",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите договор для открытия!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ViewContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractsDataGrid.SelectedItem is Contract selectedContract)
            {
                try
                {
                    using (var db = new AppDbContext())
                    {
                        var contract = db.Contracts.FirstOrDefault(c => c.Id == selectedContract.Id);
                        if (contract != null)
                        {
                            // Формируем договор с замененными плейсхолдерами
                            string documentPath = ContractWindow.GenerateContractDocumentForView(contract, db);

                            // Открываем сформированный договор
                            var wordService = new WordDocumentService();
                            wordService.OpenDocument(documentPath);
                        }
                        else
                        {
                            System.Windows.MessageBox.Show(
                                "Договор не найден!",
                                "Ошибка",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Ошибка при открытии договора: {ex.Message}",
                        "Ошибка",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите договор для просмотра!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void LoadPrograms(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                BindPrograms(FetchProgramViewModels(), GetProgramsSnapshot());
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке программ", ex, showErrors);
            }
        }

        private string? GetImagePath(string? image)
        {
            if (string.IsNullOrEmpty(image))
                return null;

            // Проверяем, является ли это URL
            if (Uri.TryCreate(image, UriKind.Absolute, out Uri? uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                // Это URL - возвращаем как есть
                return image;
            }
            // Проверяем, существует ли локальный файл
            else if (System.IO.File.Exists(image))
            {
                return image;
            }

            return null;
        }

        private void AddProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ProgramWindow();
            ((Window)window).Owner = this;
            if (((Window)window).ShowDialog() == true)
            {
                LoadPrograms();
            }
        }

        private void EditProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                using (var db = new AppDbContext())
                {
                    var program = db.LearningPrograms.Find(selectedProgram.Id);
                    if (program != null)
                    {
                        var window = new ProgramWindow(program);
                        ((Window)window).Owner = this;
                        if (((Window)window).ShowDialog() == true)
                        {
                            LoadPrograms();
                            ClearSelection();
                        }
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите программу для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeleteProgramButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить программу \"{selectedProgram.Name}\"?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var program = db.LearningPrograms.Find(selectedProgram.Id);
                            if (program != null)
                            {
                                db.LearningPrograms.Remove(program);
                                db.SaveChanges();
                                LoadPrograms();
                                ClearSelection();
                                System.Windows.MessageBox.Show(
                                    "Программа успешно удалена!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите программу для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ViewProgramDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedProgram = GetSelectedProgram();
            if (selectedProgram != null)
            {
                using (var db = new AppDbContext())
                {
                    var program = db.LearningPrograms.Find(selectedProgram.Id);
                    if (program != null)
                    {
                        var window = new ProgramDetailsWindow(program);
                        window.Owner = this;
                        window.ShowDialog();
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите программу для просмотра подробностей!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ImportProgramsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Запустить импорт программ с сайта 25-12.ru?\n\n" +
                "Это займет несколько минут.\n" +
                "Новые программы будут добавлены в базу данных.",
                "Импорт программ",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var parserWindow = new ParserWindow();
                parserWindow.Owner = this;
                bool? dialogResult = parserWindow.ShowDialog();

                // Если импорт завершен успешно, обновляем список программ
                if (dialogResult == true)
                {
                    LoadPrograms();
                    System.Windows.MessageBox.Show(
                        "Список программ обновлен!",
                        "Готово",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
        }


        private ProgramViewModel? GetSelectedProgram()
        {
            if (ProgramsItemsControl.ItemsSource != null)
            {
                foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                {
                    if (program.IsSelected)
                    {
                        return program;
                    }
                }
            }
            return null;
        }

        private void ClearSelection()
        {
            if (ProgramsItemsControl.ItemsSource != null)
            {
                foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                {
                    program.IsSelected = false;
                }
            }
        }

        private void ProgramCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // Предотвращаем всплытие события на карточку
            e.Handled = true;

            // Снимаем выделение с других программ (только одна может быть выбрана)
            if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true)
            {
                if (ProgramsItemsControl.ItemsSource != null)
                {
                    foreach (ProgramViewModel program in ProgramsItemsControl.ItemsSource)
                    {
                        if (program != checkBox.Tag as ProgramViewModel)
                        {
                            program.IsSelected = false;
                        }
                    }
                }
            }
        }

        private void ProgramCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Игнорируем клик, если он был на CheckBox
            if (e.OriginalSource is System.Windows.Controls.CheckBox)
            {
                return;
            }

            if (sender is System.Windows.FrameworkElement element && element.Tag is ProgramViewModel program)
            {
                // Переключаем выделение при двойном клике
                if (e.ClickCount == 2)
                {
                    // Двойной клик - открываем редактирование
                    ClearSelection();
                    program.IsSelected = true;
                    EditProgramButton_Click(sender, e);
                }
                else
                {
                    // Одинарный клик - просто выделяем
                    ClearSelection();
                    program.IsSelected = true;
                }
            }
        }

        private void NavigationViewItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void PersonsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonDocumentsWindow(selectedPerson);
                window.Owner = this;
                window.ShowDialog();
            }
        }

        private void LoadContractTypes()
        {
            if (!IsDbConfigured())
                return;

            try
            {
                using (var db = new AppDbContext())
                {
                    var contractTypes = db.ContractTypes.AsNoTracking().ToList();
                    ContractTypesDataGrid.ItemsSource = contractTypes;
                    _contractTypesLoaded = true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при загрузке типов договоров: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void AddContractTypeButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ContractTypeWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                LoadContractTypes();
            }
        }

        private void EditContractTypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractTypesDataGrid.SelectedItem is ContractType selectedContractType)
            {
                var window = new ContractTypeWindow(selectedContractType);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    LoadContractTypes();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите тип договора для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeleteContractTypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContractTypesDataGrid.SelectedItem is ContractType selectedContractType)
                {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить тип договора \"{selectedContractType.Name}\"?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            // Проверяем, используется ли этот тип договора
                            var contractsCount = db.Contracts.Count(c => c.ContractTypeId == selectedContractType.Id);
                            if (contractsCount > 0)
                            {
                                System.Windows.MessageBox.Show(
                                    $"Невозможно удалить тип договора, так как он используется в {contractsCount} договоре(ах)!",
                                    "Ошибка",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Warning);
                                return;
                            }

                            var contractType = db.ContractTypes.Find(selectedContractType.Id);
                            if (contractType != null)
                {
                                db.ContractTypes.Remove(contractType);
                                db.SaveChanges();
                                LoadContractTypes();
                                System.Windows.MessageBox.Show(
                                    "Тип договора успешно удален!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите тип договора для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ViewPersonDocumentsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonDocumentsWindow(selectedPerson);
                window.Owner = this;
                window.ShowDialog();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для просмотра документов!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ViewPersonCardsButton_Click(object sender, RoutedEventArgs e)
        {
            if (PersonsDataGrid.SelectedItem is Person selectedPerson)
            {
                var window = new PersonCardsWindow(selectedPerson);
                window.Owner = this;
                window.ShowDialog();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите физическое лицо для просмотра личных карточек!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void OrganizationsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void WorkloadDocumentsDataGridBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border)
            {
                var rect = new System.Windows.Rect(0, 0, border.ActualWidth, border.ActualHeight);
                border.Clip = new System.Windows.Media.RectangleGeometry(rect)
                {
                    RadiusX = 12,
                    RadiusY = 12
                };
            }
        }

        private void LoadWorkloadDocuments(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                BindWorkloadDocuments(FetchWorkloadDocuments(), GetWorkloadSnapshot());
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке учебной нагрузки", ex, showErrors);
            }
        }

        private void LoadOrganizations(bool showErrors = true)
        {
            if (!IsDbConfigured())
                return;

            try
            {
                BindOrganizations(FetchOrganizations(), GetOrganizationsSnapshot());
            }
            catch (Exception ex)
            {
                HandleLoadError("Ошибка при загрузке организаций", ex, showErrors);
            }
        }

        private void AddOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new OrganizationEditWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                LoadOrganizations();
            }
        }

        private void EditOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            if (OrganizationsDataGrid.SelectedItem is Organization selectedOrg)
            {
                var window = new OrganizationEditWindow(selectedOrg);
                window.Owner = this;
                if (window.ShowDialog() == true)
                {
                    LoadOrganizations();
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите организацию для редактирования!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void DeleteOrganizationButton_Click(object sender, RoutedEventArgs e)
        {
            if (OrganizationsDataGrid.SelectedItem is Organization selectedOrg)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Вы уверены, что хотите удалить организацию '{selectedOrg.OrganizationName}'?",
                    "Подтверждение удаления",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var db = new AppDbContext())
                        {
                            var orgToDelete = db.Organizations.Find(selectedOrg.Id);
                            if (orgToDelete != null)
                            {
                                db.Organizations.Remove(orgToDelete);
                                db.SaveChanges();
                                LoadOrganizations();
                                System.Windows.MessageBox.Show(
                                    "Организация успешно удалена!",
                                    "Успех",
                                    System.Windows.MessageBoxButton.OK,
                                    System.Windows.MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"Ошибка при удалении организации: {ex.Message}",
                            "Ошибка",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Выберите организацию для удаления!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Останавливаем таймер при закрытии окна
            if (_autoRefreshTimer != null)
            {
                _autoRefreshTimer.Stop();
                _autoRefreshTimer = null;
                System.Diagnostics.Debug.WriteLine("Автообновление остановлено");
            }

            if (AiCourseDraftHost.Content is AiCourseDraftControl aiCourseDraftControl)
            {
                aiCourseDraftControl.Shutdown();
            }
            
            base.OnClosing(e);
        }

        private void EnsureAiCourseDraftControlLoaded()
        {
            if (AiCourseDraftHost.Content is AiCourseDraftControl)
            {
                return;
            }

            try
            {
                var control = new AiCourseDraftControl
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                AiCourseDraftHost.Content = control;
            }
            catch (Exception ex)
            {
                var errorText = new System.Windows.Controls.TextBox
                {
                    Text =
                        "Не удалось загрузить вкладку 'Формирование новых курсов'." + Environment.NewLine +
                        Environment.NewLine +
                        ex.ToString(),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(24),
                };

                AiCourseDraftHost.Content = errorText;
            }
        }

        private void EnsureOrdersControlLoaded()
        {
            if (OrdersHost.Content is OrdersControl)
            {
                return;
            }

            try
            {
                var control = new OrdersControl
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                OrdersHost.Content = control;
            }
            catch (Exception ex)
            {
                var errorText = new System.Windows.Controls.TextBox
                {
                    Text =
                        "Не удалось загрузить вкладку 'Приказы'." + Environment.NewLine +
                        Environment.NewLine +
                        ex.ToString(),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(24),
                };

                OrdersHost.Content = errorText;
            }
        }

        private void PersonsDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void CreateWorkloadButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new WorkloadWindow();
            window.Owner = this;
            var result = window.ShowDialog();
            if (result == true)
            {
                LoadWorkloadDocuments();
            }
        }

        private void OpenHolidayCalendarButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new HolidayCalendarWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void OpenWorkloadDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkloadDocumentsDataGrid.SelectedItem is not WorkloadDocument selectedDocument)
            {
                System.Windows.MessageBox.Show(
                    "Выберите файл учебной нагрузки!",
                    "Внимание",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedDocument.FilePath) || !File.Exists(selectedDocument.FilePath))
            {
                System.Windows.MessageBox.Show(
                    "Файл не найден. Возможно, он был удален или перемещен.",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                var excelService = new ExcelDocumentService();
                excelService.OpenDocument(selectedDocument.FilePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка при открытии файла: {ex.Message}",
                    "Ошибка",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void RefreshWorkloadDocumentsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadWorkloadDocuments();
        }
    }

    // Класс для отображения программ в карточках
    public class ProgramViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Format { get; set; } = "";
        public int Hours { get; set; }
        public int LessonsCount { get; set; }
        public decimal Price { get; set; }
        public string? ImagePath { get; set; }
        public string ProgramViewName { get; set; } = "";

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
