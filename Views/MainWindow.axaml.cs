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
        private readonly TicketRepository _repo = new();
        private List<Ticket> _recentTicketBacking = new();

        public ObservableCollection<Ticket> Tickets { get; } = new();

        private readonly UserRepository _userRepo = new();
        private List<User> _users = new();

        public static int CurrentUserId { get; private set; } = 1;
        public static string CurrentUserRole { get; private set; } = "User";

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

            if (AllTicketsList != null)
            {
                AllTicketsList.ItemsSource = Tickets
                    .OrderByDescending(t => t.Vytvoreno)
                    .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Vytvoreno:g}")
                    .ToList();
            }
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

            AllTicketsList.ItemsSource = Tickets
                .OrderByDescending(t => t.Vytvoreno)
                .Select(t => $"#{t.Id} | {t.Nadpis} | {t.Status} | {t.Vytvoreno:g}")
                .ToList();
        }

        private void RefreshData_Click(object? sender, RoutedEventArgs e)
        {
            LoadTickets();
            RefreshDashboard();
            BuildChart();
        }

        private async void RecentTicketsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (RecentTicketsList.SelectedIndex < 0 || RecentTicketsList.SelectedIndex >= _recentTicketBacking.Count)
                return;

            var selected = _recentTicketBacking[RecentTicketsList.SelectedIndex];
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

            if (AllTicketsList.SelectedIndex < 0)
                return;

            var ordered = Tickets.OrderByDescending(t => t.Vytvoreno).ToList();
            if (AllTicketsList.SelectedIndex >= ordered.Count)
                return;

            var selected = ordered[AllTicketsList.SelectedIndex];
            var detailWindow = new TicketDetailWindow(selected);
            await detailWindow.ShowDialog(this);

            if (detailWindow.Changed)
            {
                LoadTickets();
                RefreshDashboard();
                BuildChart();
                OpenAllTickets_Click(null, new RoutedEventArgs());
            }
        }

        private void SaveTicket_Click(object? sender, RoutedEventArgs e)
        {
            var nadpis = NadpisInput.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(nadpis))
                return;

            var priorita = ((PrioritaInput.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? "Střední";

            var ticket = new Ticket
            {
                Nadpis = nadpis,
                Popisek = PopisekInput.Text ?? "",
                Status = "Otevřený",
                Priorita = priorita,
                Kategorie = KategorieInput.Text ?? "",
                VytvorenoUzivatelem = CurrentUserId,
                PridelenoUzivatelem = null
            };

            _repo.Insert(ticket);

            ClearNewTicketForm_Click(null, new RoutedEventArgs());
            LoadTickets();
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
            KategorieInput.Text = "";
            PrioritaInput.SelectedIndex = 1;
        }

        private void CurrentUserCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Legacy věc - upravit uživatele se nedá, ale combobox zůstalo. Kdyby se náhodou změnil přihlášený uživatel, tak se to tady zachytí.
        }
    }
}