using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem
{
    public partial class TicketDetailWindow : Window
    {
        private const int MaxCommentLength = 1000;

        private readonly TicketRepository _ticketRepo = new();
        private readonly TicketCommentRepository _commentRepo = new();
        private readonly Ticket _ticket;
        private readonly UserRepository _userRepo = new();

        public bool Changed { get; private set; }

        public TicketDetailWindow() : this(new Ticket())
        {
        }

        public TicketDetailWindow(Ticket ticket)
        {
            InitializeComponent();
            _ticket = ticket;
            FillData();
            LoadComments();
            var isAdmin = string.Equals(MainWindow.CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);
            DeleteTicketButton.IsVisible = isAdmin;
        }

        private void FillData()
        {
            TitleText.Text = _ticket.Nadpis;
            IdText.Text = _ticket.Id.ToString();
            StatusPriorityText.Text = $"{_ticket.Status} / {_ticket.Priorita}";
            OwnerText.Text = _userRepo.GetNameById(_ticket.VytvorenoUzivatelem);
            CategoryText.Text = string.IsNullOrWhiteSpace(_ticket.Kategorie) ? "-" : _ticket.Kategorie;
            DescriptionText.Text = string.IsNullOrWhiteSpace(_ticket.Popisek) ? "-" : _ticket.Popisek;

            var isAdmin = string.Equals(MainWindow.CurrentUserRole, "Admin", StringComparison.OrdinalIgnoreCase);
            CloseTicketButton.IsEnabled = isAdmin && _ticket.Status != "Uzavřený";
        }

        private void LoadComments()
        {
            var comments = _commentRepo.GetByTicketId(_ticket.Id);
            CommentsList.ItemsSource = comments.Select(c => $"{c.Vytvoreno:g} | {c.AutorJmeno}: {c.Text}").ToList();
        }

        private void AddCommentButton_Click(object? sender, RoutedEventArgs e)
        {
            OperationErrorText.Text = "";

            var text = (NewCommentTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                OperationErrorText.Text = "Komentář nesmí být prázdný.";
                return;
            }

            if (text.Length > MaxCommentLength)
            {
                OperationErrorText.Text = $"Komentář může mít maximálně {MaxCommentLength} znaků.";
                return;
            }

            try
            {
                _commentRepo.Add(_ticket.Id, MainWindow.CurrentUserId, text);
            }
            catch (ArgumentException ex)
            {
                OperationErrorText.Text = ex.Message;
                return;
            }

            NewCommentTextBox.Text = "";
            LoadComments();
            Changed = true;
        }

        private void CloseTicketButton_Click(object? sender, RoutedEventArgs e)
        {
            OperationErrorText.Text = "";

            try
            {
                _ticketRepo.CloseTicket(_ticket.Id, MainWindow.CurrentUserRole);
            }
            catch (UnauthorizedAccessException ex)
            {
                OperationErrorText.Text = ex.Message;
                return;
            }

            Changed = true;
            Close();
        }

        private void DeleteTicket_Click(object? sender, RoutedEventArgs e)
        {
            OperationErrorText.Text = "";

            try
            {
                _ticketRepo.Delete(_ticket.Id, MainWindow.CurrentUserRole);
            }
            catch (UnauthorizedAccessException ex)
            {
                OperationErrorText.Text = ex.Message;
                return;
            }

            Changed = true;
            Close();
        }

        private void CloseWindow_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}