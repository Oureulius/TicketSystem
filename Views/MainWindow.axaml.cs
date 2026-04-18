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

        public MainWindow()
        {
            InitializeComponent();
        }

        public void InitializeForUser(User user)
        {
            ApplyAuthenticatedUser(user);
            LoadTickets();
            RefreshDashboard();
            BuildChart();
        }

        private void ApplyAuthenticatedUser(User user)
        {
            CurrentUserId = user.Id;
            CurrentUserRole = user.Role;

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
            var isAdmin = string.Equals(CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);
            AllTicketsButton.IsVisible = isAdmin;
        }

        private bool IsAdmin()
            => string.Equals(CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);

        private IEnumerable<Ticket> GetVisibleTickets(IEnumerable<Ticket> allTickets)
        {
            if (IsAdmin())
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

            /*if (AllTicketsList != null)
            {
                AllTicketsList.ItemsSource = Tickets
                    .OrderByDescending(t => t.Vytvoreno)
                    .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Vytvoreno:g}")
                    .ToList();
            }*/
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
            DashboardView.IsVisible = true;
            NewTicketView.IsVisible = false;
            AllTicketsView.IsVisible = false;
        }

        private void OpenNewTicket_Click(object? sender, RoutedEventArgs e)
        {
            DashboardView.IsVisible = false;
            NewTicketView.IsVisible = true;
            AllTicketsView.IsVisible = false;
        }

        private void OpenAllTickets_Click(object? sender, RoutedEventArgs e)
        {
            if (!IsAdmin())
            {
                CurrentUserInfoText.Text = "Přístup zamítnut: seznam všech ticketů je jen pro roli Admin.";
                return;
            }

            DashboardView.IsVisible = false;
            NewTicketView.IsVisible = false;
            AllTicketsView.IsVisible = true;

            InitializeAllTicketsFilters();
            ReloadAllTicketsFromDb();
        }

        private void RefreshData_Click(object? sender, RoutedEventArgs e)
        {
            LoadTickets();
            RefreshDashboard();
            BuildChart();
            ReloadAllTicketsFromDb();
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
                RefreshDashboard();
                BuildChart();
            }
        }

        private async void AllTicketsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsAdmin())
                return;

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
                RefreshDashboard();
                BuildChart();
                ReloadAllTicketsFromDb();
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
            ReloadAllTicketsFromDb();
            RefreshDashboard();
            BuildChart();

            DashboardView.IsVisible = true;
            NewTicketView.IsVisible = false;
            AllTicketsView.IsVisible = false;
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
            if (!IsAdmin())
                return;

            _isInitializingAllTicketsFilters = true;
            try
            {
                var priorities = new List<string> { "Vše" };
                priorities.AddRange(_repo.GetDistinctPriorities());

                var categories = new List<string> { "Vše" };
                categories.AddRange(_repo.GetDistinctCategories());

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

                AllPriorityFilter.ItemsSource = priorities;
                AllCategoryFilter.ItemsSource = categories;
                AllCreatorFilter.ItemsSource = creators;

                AllPriorityFilter.SelectedIndex = 0;
                AllCategoryFilter.SelectedIndex = 0;
                AllCreatorFilter.SelectedIndex = 0;
            }
            finally
            {
                _isInitializingAllTicketsFilters = false;
            }
        }

        private void ReloadAllTicketsFromDb()
        {
            if (!IsAdmin())
                return;

            var priorita = AllPriorityFilter.SelectedItem as string;
            if (string.Equals(priorita, "Vše", StringComparison.Ordinal))
                priorita = null;

            var kategorie = AllCategoryFilter.SelectedItem as string;
            if (string.Equals(kategorie, "Vše", StringComparison.Ordinal))
                kategorie = null;

            var creator = AllCreatorFilter.SelectedItem as CreatorFilterOption;
            var creatorId = creator?.UserId;

            _allTicketsBacking = _repo.GetFiltered(priorita, kategorie, creatorId);

            AllTicketsList.ItemsSource = _allTicketsBacking
                .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Priorita} | {t.Kategorie} | {t.Vytvoreno:g}")
                .ToList();
        }

        private void AllTicketsFilter_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!IsAdmin() || _isInitializingAllTicketsFilters)
                return;

            ReloadAllTicketsFromDb();
        }

        private void ClearAllTicketsFilters_Click(object? sender, RoutedEventArgs e)
        {
            if (!IsAdmin())
                return;

            AllPriorityFilter.SelectedIndex = 0;
            AllCategoryFilter.SelectedIndex = 0;
            AllCreatorFilter.SelectedIndex = 0;

            ReloadAllTicketsFromDb();
        }
    }
}