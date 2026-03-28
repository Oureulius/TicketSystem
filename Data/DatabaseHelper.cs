using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TicketSystem.Data
{
    public static class DatabaseHelper
    {
        private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "tickets.db");
        public static string ConnectionString => $"Data Source={DbPath};Version=3;";

        public static void Initialize()
        {
            var isNewDb = !File.Exists(DbPath);
            if (isNewDb)
                SQLiteConnection.CreateFile(DbPath);

            using var conn = new SQLiteConnection(ConnectionString);
            conn.Open();

            using (var pragmaCmd = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
            {
                pragmaCmd.ExecuteNonQuery();
            }

            using var tx = conn.BeginTransaction();

            using (var usersCmd = new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    Jmeno      TEXT NOT NULL,
                    Email      TEXT NOT NULL UNIQUE,
                    Role       TEXT NOT NULL DEFAULT 'User',
                    Login      TEXT,
                    HesloHash  TEXT
                );", conn, tx))
            {
                usersCmd.ExecuteNonQuery();
            }

            EnsureUserAuthColumns(conn, tx);

            using (var ticketsCmd = new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS Tickets (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    Nadpis           TEXT NOT NULL,
                    Popisek          TEXT,
                    Status           TEXT NOT NULL DEFAULT 'Otevřený',
                    Priorita         TEXT NOT NULL DEFAULT 'Střední',
                    Kategorie        TEXT,
                    Vytvoreno        TEXT NOT NULL DEFAULT (datetime('now')),
                    ZmenaCasu        TEXT,
                    VytvorenoUzivatelem  INTEGER NOT NULL,
                    PridelenoUzivatelem INTEGER,
                    FOREIGN KEY (VytvorenoUzivatelem) REFERENCES Users(Id),
                    FOREIGN KEY (PridelenoUzivatelem) REFERENCES Users(Id)
                );", conn, tx))
            {
                ticketsCmd.ExecuteNonQuery();
            }

            using (var commentsCmd = new SQLiteCommand(@"
                CREATE TABLE IF NOT EXISTS TicketComments (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    TicketId   INTEGER NOT NULL,
                    UserId     INTEGER NOT NULL,
                    Text       TEXT NOT NULL,
                    Vytvoreno  TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (TicketId) REFERENCES Tickets(Id) ON DELETE CASCADE,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );", conn, tx))
            {
                commentsCmd.ExecuteNonQuery();
            }

            SeedDefaultUsers(conn, tx);

            tx.Commit();
        }

        public static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        private static void EnsureUserAuthColumns(SQLiteConnection conn, SQLiteTransaction tx)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = new SQLiteCommand("PRAGMA table_info(Users);", conn, tx))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var name = r["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        columns.Add(name);
                }
            }

            if (!columns.Contains("Login"))
            {
                using var addLoginCmd = new SQLiteCommand("ALTER TABLE Users ADD COLUMN Login TEXT;", conn, tx);
                addLoginCmd.ExecuteNonQuery();
            }

            if (!columns.Contains("HesloHash"))
            {
                using var addPassCmd = new SQLiteCommand("ALTER TABLE Users ADD COLUMN HesloHash TEXT;", conn, tx);
                addPassCmd.ExecuteNonQuery();
            }
        }

        private static void SeedDefaultUsers(SQLiteConnection conn, SQLiteTransaction tx)
        {
            UpsertUser(conn, tx,
                jmeno: "Admin",
                email: "admin@tickets.local",
                role: "Admin",
                login: "admin",
                hesloHash: HashPassword("admin123"));

            UpsertUser(conn, tx,
                jmeno: "User",
                email: "user@tickets.local",
                role: "User",
                login: "user",
                hesloHash: HashPassword("user123"));
        }

        private static void UpsertUser(
            SQLiteConnection conn,
            SQLiteTransaction tx,
            string jmeno,
            string email,
            string role,
            string login,
            string hesloHash)
        {
            using (var insertCmd = new SQLiteCommand(@"
                INSERT INTO Users (Jmeno, Email, Role, Login, HesloHash)
                SELECT @jmeno, @email, @role, @login, @hesloHash
                WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Email = @email);", conn, tx))
            {
                insertCmd.Parameters.AddWithValue("@jmeno", jmeno);
                insertCmd.Parameters.AddWithValue("@email", email);
                insertCmd.Parameters.AddWithValue("@role", role);
                insertCmd.Parameters.AddWithValue("@login", login);
                insertCmd.Parameters.AddWithValue("@hesloHash", hesloHash);
                insertCmd.ExecuteNonQuery();
            }

            using (var updateCmd = new SQLiteCommand(@"
                UPDATE Users
                SET Jmeno = @jmeno,
                    Role = @role,
                    Login = @login,
                    HesloHash = @hesloHash
                WHERE Email = @email;", conn, tx))
            {
                updateCmd.Parameters.AddWithValue("@jmeno", jmeno);
                updateCmd.Parameters.AddWithValue("@email", email);
                updateCmd.Parameters.AddWithValue("@role", role);
                updateCmd.Parameters.AddWithValue("@login", login);
                updateCmd.Parameters.AddWithValue("@hesloHash", hesloHash);
                updateCmd.ExecuteNonQuery();
            }
        }
    }
}