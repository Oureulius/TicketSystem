using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem
{
    public partial class MainWindow : Window
    {
        private const int MaxTitleLength = 120;
        private const int MaxDescriptionLength = 2000;

        private readonly TicketRepository _repo = new();
        private List<Ticket> _recentTicketBacking = new();
        private List<Ticket> _allTicketsBacking = new();

        public ObservableCollection<Ticket> Tickets { get; } = new();

        private readonly UserRepository _userRepo = new();
        private List<User> _users = new();

        public static int CurrentUserId { get; private set; } = 1;
        public static string CurrentUserRole { get; private set; } = "User";

        private bool _isInitializingAllTicketsFilters;
        private bool _isAdmin;

        public MainWindow()
        {
            InitializeComponent();

            if (StatisticsTimelineRangeCombo != null)
                StatisticsTimelineRangeCombo.SelectionChanged += StatisticsTimelineRangeCombo_SelectionChanged;
        }

        public void InitializeForUser(User user)
        {
            ApplyAuthenticatedUser(user);
            LoadTickets();
            RefreshAll(buildChart: true, refreshStatistics: false, reloadAllTickets: false);
        }

        private void ApplyAuthenticatedUser(User user)
        {
            CurrentUserId = user.Id;
            CurrentUserRole = user.Role;
            _isAdmin = string.Equals(CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);

            _users = _userRepo.GetAll();
            CurrentUserCombo.ItemsSource = _users;

            var idx = _users.FindIndex(u => u.Id == user.Id);
            if (idx < 0) idx = 0;

            CurrentUserCombo.SelectedIndex = idx;
            CurrentUserCombo.IsEnabled = false;
            CurrentUserInfoText.Text = $"Přihlášen: {user.Jmeno} ({user.Role})";

            ApplyRolePermissions();
        }

        private void ApplyRolePermissions()
        {
            AllTicketsButton.IsVisible = true;
            StatisticsButton.IsVisible = _isAdmin;
            AddUserButton.IsVisible = _isAdmin;

            if (!_isAdmin)
            {
                StatisticsView.IsVisible = false;
                AddUserView.IsVisible = false;
            }

            AllPriorityFilter.IsEnabled = true;
            AllCategoryFilter.IsEnabled = true;
            AllCreatorFilter.IsEnabled = _isAdmin;
            AllCreatorFilter.IsVisible = _isAdmin;
        }

        private bool IsAdmin() => _isAdmin;

        private IEnumerable<Ticket> GetVisibleTickets(IEnumerable<Ticket> allTickets)
        {
            if (_isAdmin)
                return allTickets;

            return allTickets.Where(t =>
                t.VytvorenoUzivatelem == CurrentUserId ||
                (t.PridelenoUzivatelem.HasValue && t.PridelenoUzivatelem.Value == CurrentUserId));
        }

        private void LoadTickets()
        {
            Tickets.Clear();

            var allTickets = _repo.GetAll();
            var visibleTickets = GetVisibleTickets(allTickets);

            foreach (var t in visibleTickets)
                Tickets.Add(t);

            _recentTicketBacking = Tickets
                .OrderByDescending(t => t.Vytvoreno)
                .Take(20)
                .ToList();

            if (RecentTicketsList != null)
            {
                RecentTicketsList.ItemsSource = _recentTicketBacking
                    .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Vytvoreno:g}")
                    .ToList();
            }
        }

        private void RefreshAll(bool reloadAllTickets, bool refreshStatistics, bool buildChart)
        {
            RefreshDashboard();

            if (buildChart)
                BuildChart();

            if (refreshStatistics && _isAdmin)
                RefreshStatistics();

            if (reloadAllTickets)
                ReloadAllTicketsFromDb();
        }

        private void ShowView(Control? viewToShow)
        {
            DashboardView.IsVisible = viewToShow == DashboardView;
            NewTicketView.IsVisible = viewToShow == NewTicketView;
            AllTicketsView.IsVisible = viewToShow == AllTicketsView;
            StatisticsView.IsVisible = viewToShow == StatisticsView;
            AddUserView.IsVisible = viewToShow == AddUserView;
        }

        private void RefreshDashboard()
        {
            var total = Tickets.Count;
            var open = Tickets.Count(t => t.Status == "Otevřený");
            var closed = Tickets.Count(t => t.Status == "Uzavřený");
            var newToday = Tickets.Count(t => (DateTime.Now - t.Vytvoreno).TotalHours <= 24);

            TotalTicketsText.Text = total.ToString();
            OpenTicketsText.Text = open.ToString();
            ClosedTicketsText.Text = closed.ToString();
            NewTodayText.Text = newToday.ToString();
        }

        private void BuildChart()
        {
            var days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var counts = days
                .Select(day => Tickets.Count(t => t.Vytvoreno.Date == day.Date))
                .Select(c => (double)c)
                .ToArray();

            TicketsChart.Series = new ISeries[]
            {
                new ColumnSeries<double> { Values = counts }
            };

            TicketsChart.XAxes = new Axis[]
            {
                new Axis { Labels = days.Select(d => d.ToString("dd.MM")).ToArray() }
            };

            TicketsChart.YAxes = new Axis[]
            {
                new Axis { MinLimit = 0 }
            };
        }

        private void OpenDashboard_Click(object? sender, RoutedEventArgs e)
        {
            ShowView(DashboardView);
        }

        private void OpenNewTicket_Click(object? sender, RoutedEventArgs e)
        {
            ShowView(NewTicketView);
        }

        private void OpenAllTickets_Click(object? sender, RoutedEventArgs e)
        {
            ShowView(AllTicketsView);

            InitializeAllTicketsFilters();
            ReloadAllTicketsFromDb();
        }

        private void OpenStatistics_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                CurrentUserInfoText.Text = "Přístup zamítnut: statistika je jen pro roli Admin.";
                return;
            }

            ShowView(StatisticsView);
            RefreshStatistics();
        }

        private void RefreshData_Click(object? sender, RoutedEventArgs e)
        {
            LoadTickets();
            RefreshAll(reloadAllTickets: true, refreshStatistics: true, buildChart: true);
        }

        private async void RecentTicketsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedIndex = RecentTicketsList.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _recentTicketBacking.Count)
                return;

            var selected = _recentTicketBacking[selectedIndex];
            RecentTicketsList.SelectedIndex = -1;

            var detailWindow = new TicketDetailWindow(selected);
            await detailWindow.ShowDialog(this);

            if (detailWindow.Changed)
            {
                LoadTickets();
                RefreshAll(reloadAllTickets: false, refreshStatistics: true, buildChart: true);
            }
        }

        private async void AllTicketsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedIndex = AllTicketsList.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _allTicketsBacking.Count)
                return;

            var selected = _allTicketsBacking[selectedIndex];
            AllTicketsList.SelectedIndex = -1;

            var detailWindow = new TicketDetailWindow(selected);
            await detailWindow.ShowDialog(this);

            if (detailWindow.Changed)
            {
                LoadTickets();
                RefreshAll(reloadAllTickets: true, refreshStatistics: true, buildChart: true);
            }
        }

        private void SaveTicket_Click(object? sender, RoutedEventArgs e)
        {
            NewTicketErrorText.Text = "";

            var nadpis = (NadpisInput.Text ?? "").Trim();
            var popisek = (PopisekInput.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(nadpis))
            {
                NewTicketErrorText.Text = "Nadpis je povinný.";
                return;
            }

            if (nadpis.Length > MaxTitleLength)
            {
                NewTicketErrorText.Text = $"Nadpis může mít maximálně {MaxTitleLength} znaků.";
                return;
            }

            if (popisek.Length > MaxDescriptionLength)
            {
                NewTicketErrorText.Text = $"Popis může mít maximálně {MaxDescriptionLength} znaků.";
                return;
            }

            var priorita = ((PrioritaInput.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Střední";
            var kategorie = ((Kategorie.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Kancelář";

            var ticket = new Ticket
            {
                Nadpis = nadpis,
                Popisek = popisek,
                Status = "Otevřený",
                Priorita = priorita,
                Kategorie = kategorie,
                VytvorenoUzivatelem = CurrentUserId,
                PridelenoUzivatelem = null
            };

            try
            {
                _repo.Insert(ticket);
            }
            catch (ArgumentException ex)
            {
                NewTicketErrorText.Text = ex.Message;
                return;
            }

            ClearNewTicketForm_Click(null, new RoutedEventArgs());
            LoadTickets();
            RefreshAll(reloadAllTickets: true, refreshStatistics: true, buildChart: true);

            ShowView(DashboardView);
        }

        private void ClearNewTicketForm_Click(object? sender, RoutedEventArgs e)
        {
            NadpisInput.Text = "";
            PopisekInput.Text = "";
            PrioritaInput.SelectedIndex = 1;
            NewTicketErrorText.Text = "";
        }

        private void CurrentUserCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Legacy věc - upravit uživatele se nedá, ale combobox zůstalo. Kdyby se náhodou změnil přihlášený uživatel, tak se to tady zachytí.
        }

        private void InitializeAllTicketsFilters()
        {
            _isInitializingAllTicketsFilters = true;
            try
            {
                var priorities = new List<string> { "Vše" };
                priorities.AddRange(_repo.GetDistinctPriorities());

                var categories = new List<string> { "Vše" };
                categories.AddRange(_repo.GetDistinctCategories());

                AllPriorityFilter.ItemsSource = priorities;
                AllCategoryFilter.ItemsSource = categories;

                AllPriorityFilter.SelectedIndex = 0;
                AllCategoryFilter.SelectedIndex = 0;

                if (_isAdmin)
                {
                    var creators = new List<CreatorFilterOption>
                    {
                        new CreatorFilterOption { UserId = null, Label = "Všichni uživatelé" }
                    };

                    creators.AddRange(_users
                        .OrderBy(u => u.Jmeno)
                        .Select(u => new CreatorFilterOption
                        {
                            UserId = u.Id,
                            Label = $"{u.Jmeno} ({u.Role})"
                        }));

                    AllCreatorFilter.ItemsSource = creators;
                    AllCreatorFilter.SelectedIndex = 0;
                }
            }
            finally
            {
                _isInitializingAllTicketsFilters = false;
            }
        }

        private void ReloadAllTicketsFromDb()
        {
            var priorita = AllPriorityFilter.SelectedItem as string;
            if (string.Equals(priorita, "Vše", StringComparison.Ordinal))
                priorita = null;

            var kategorie = AllCategoryFilter.SelectedItem as string;
            if (string.Equals(kategorie, "Vše", StringComparison.Ordinal))
                kategorie = null;

            if (_isAdmin)
            {
                var creator = AllCreatorFilter.SelectedItem as CreatorFilterOption;
                var creatorId = creator?.UserId;

                _allTicketsBacking = _repo.GetFiltered(priorita, kategorie, creatorId);
            }
            else
            {
                _allTicketsBacking = GetVisibleTickets(_repo.GetFiltered(priorita, kategorie, null))
                    .OrderByDescending(t => t.Vytvoreno)
                    .ToList();
            }

            AllTicketsList.ItemsSource = _allTicketsBacking
                .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Priorita} | {t.Kategorie} | {t.Vytvoreno:g}")
                .ToList();
        }

        private void AllTicketsFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingAllTicketsFilters)
                return;

            ReloadAllTicketsFromDb();
        }

        private void ClearAllTicketsFilters_Click(object? sender, RoutedEventArgs e)
        {
            AllPriorityFilter.SelectedIndex = 0;
            AllCategoryFilter.SelectedIndex = 0;

            if (_isAdmin)
                AllCreatorFilter.SelectedIndex = 0;

            ReloadAllTicketsFromDb();
        }

        private void StatisticsTimelineRangeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            BuildStatisticsTimelineChart();
        }

        private void RefreshStatistics()
        {
            RefreshStatisticsSummary();
            BuildStatisticsTimelineChart();
            BuildMostFrequentProblems();
        }

        private void RefreshStatisticsSummary()
        {
            var now = DateTime.Now;

            StatsLast1DayText.Text = Tickets.Count(t => t.Vytvoreno >= now.AddDays(-1)).ToString();
            StatsLast14DaysText.Text = Tickets.Count(t => t.Vytvoreno >= now.AddDays(-14)).ToString();
            StatsLast30DaysText.Text = Tickets.Count(t => t.Vytvoreno >= now.AddDays(-30)).ToString();
            StatsLast365DaysText.Text = Tickets.Count(t => t.Vytvoreno >= now.AddDays(-365)).ToString();
        }

        private void BuildStatisticsTimelineChart()
        {
            if (StatisticsTimelineChart is null)
                return;

            var daysCount = GetSelectedTimelineRangeDays();
            var startDay = DateTime.Today.AddDays(-(daysCount - 1));

            var days = Enumerable.Range(0, daysCount)
                .Select(i => startDay.AddDays(i))
                .ToList();

            var counts = days
                .Select(day => (double)Tickets.Count(t => t.Vytvoreno.Date == day.Date))
                .ToArray();

            StatisticsTimelineChart.Series = new ISeries[]
            {
                new ColumnSeries<double> { Values = counts }
            };

            StatisticsTimelineChart.XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = days.Select(d => d.ToString("dd.MM")).ToArray(),
                    LabelsRotation = daysCount > 30 ? 45 : 0
                }
            };

            StatisticsTimelineChart.YAxes = new Axis[]
            {
                new Axis { MinLimit = 0 }
            };
        }

        private int GetSelectedTimelineRangeDays()
        {
            var selected = (StatisticsTimelineRangeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (int.TryParse(selected, out var days) && days > 0)
                return days;

            return 30;
        }

        private void BuildMostFrequentProblems()
        {
            var topProblems = Tickets
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Kategorie) ? "Nezařazené" : t.Kategorie.Trim())
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(10)
                .Select(g => $"{g.Key} - {g.Count()}x")
                .ToList();

            if (topProblems.Count == 0)
                topProblems.Add("Bez dat");

            TopProblemsList.ItemsSource = topProblems;
        }

        private void TopProblemsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
                listBox.SelectedIndex = -1;
        }

        private void Logout_Click(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                Close();
                return;
            }

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = new LoginWindow();
            desktop.MainWindow = loginWindow;

            loginWindow.Closed += (_, _) =>
            {
                var user = loginWindow.AuthenticatedUser;
                if (user is null)
                {
                    desktop.Shutdown();
                    return;
                }

                var mainWindow = new MainWindow();
                mainWindow.InitializeForUser(user);

                desktop.MainWindow = mainWindow;
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            };

            Close();
            loginWindow.Show();
        }

        private void OpenAddUser_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                CurrentUserInfoText.Text = "Přístup zamítnut: správu uživatelů má jen Admin.";
                return;
            }

            ShowView(AddUserView);
            AddUserErrorText.Text = "";
            RefreshUsersList();
        }

        private void ClearNewUserForm_Click(object? sender, RoutedEventArgs e)
        {
            NewUserNameInput.Text = "";
            NewUserPasswordInput.Text = "";
            NewUserRoleCombo.SelectedIndex = 0;
            AddUserErrorText.Text = "";
        }

        private void CreateUser_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                AddUserErrorText.Text = "Uživatele může vytvářet jen Admin.";
                return;
            }

            AddUserErrorText.Text = "";

            var jmeno = (NewUserNameInput.Text ?? "").Trim();
            var heslo = NewUserPasswordInput.Text ?? "";
            var role = (NewUserRoleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "User";

            try
            {
                _userRepo.CreateByAdmin(jmeno, heslo, role);

                _users = _userRepo.GetAll();
                CurrentUserCombo.ItemsSource = _users;

                var idx = _users.FindIndex(u => u.Id == CurrentUserId);
                if (idx >= 0)
                    CurrentUserCombo.SelectedIndex = idx;

                AddUserErrorText.Text = "Uživatel byl úspěšně vytvořen.";
                ClearNewUserForm_Click(null, new RoutedEventArgs());
                RefreshUsersList();
            }
            catch (ArgumentException ex)
            {
                AddUserErrorText.Text = ex.Message;
            }
        }

        private void DeleteUser_Click(object? sender, RoutedEventArgs e)
        {
            if (!_isAdmin)
            {
                AddUserErrorText.Text = "Uživatele může mazat jen Admin.";
                return;
            }

            var selectedIndex = UsersList.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _users.Count)
            {
                AddUserErrorText.Text = "Vyber uživatele ke smazání.";
                return;
            }

            var selected = _users[selectedIndex];
            if (selected.Id == CurrentUserId)
            {
                AddUserErrorText.Text = "Nelze smazat aktuálně přihlášeného uživatele.";
                return;
            }

            try
            {
                _userRepo.DeleteByAdmin(selected.Id);
                RefreshUsersList();
                AddUserErrorText.Text = "Uživatel byl smazán.";
            }
            catch (ArgumentException ex)
            {
                AddUserErrorText.Text = ex.Message;
            }
        }

        private void RefreshUsersList()
        {
            _users = _userRepo.GetAll();
            UsersList.ItemsSource = _users
                .Select(u => $"{u.Id} | {u.Jmeno} | {u.Role} | {u.Login}")
                .ToList();
        }
    }
}