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
                    HesloHash  TEXT,
                    HesloSalt  TEXT
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
                    Vytvoreno        TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
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

        public static string GenerateSalt(int size = 16)
        {
            var salt = RandomNumberGenerator.GetBytes(size);
            return Convert.ToHexString(salt);
        }

        public static string HashPassword(string password, string saltHex)
        {
            var saltBytes = Convert.FromHexString(saltHex);
            var passwordBytes = Encoding.UTF8.GetBytes(password);

            var data = new byte[saltBytes.Length + passwordBytes.Length];
            Buffer.BlockCopy(saltBytes, 0, data, 0, saltBytes.Length);
            Buffer.BlockCopy(passwordBytes, 0, data, saltBytes.Length, passwordBytes.Length);

            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash);
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

            if (!columns.Contains("HesloSalt"))
            {
                using var addSaltCmd = new SQLiteCommand("ALTER TABLE Users ADD COLUMN HesloSalt TEXT;", conn, tx);
                addSaltCmd.ExecuteNonQuery();
            }
        }

        private static void SeedDefaultUsers(SQLiteConnection conn, SQLiteTransaction tx)
        {
            UpsertUser(conn, tx, "Admin", "admin@tickets.local", "Admin", "admin", "admin123", forceResetPassword: false);
            UpsertUser(conn, tx, "User", "user@tickets.local", "User", "user", "user123", forceResetPassword: false);
        }

        private static void UpsertUser(
            SQLiteConnection conn,
            SQLiteTransaction tx,
            string jmeno,
            string email,
            string role,
            string login,
            string heslo,
            bool forceResetPassword = false)
        {
            var salt = GenerateSalt();
            var hesloHash = HashPassword(heslo, salt);

            using var insertCmd = new SQLiteCommand(@"
                INSERT INTO Users (Jmeno, Email, Role, Login, HesloHash, HesloSalt)
                SELECT @jmeno, @email, @role, @login, @hesloHash, @hesloSalt
                WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Email = @email);", conn, tx);

            insertCmd.Parameters.AddWithValue("@jmeno", jmeno);
            insertCmd.Parameters.AddWithValue("@email", email);
            insertCmd.Parameters.AddWithValue("@role", role);
            insertCmd.Parameters.AddWithValue("@login", login);
            insertCmd.Parameters.AddWithValue("@hesloHash", hesloHash);
            insertCmd.Parameters.AddWithValue("@hesloSalt", salt);

            var inserted = insertCmd.ExecuteNonQuery() > 0;

            using var updateProfileCmd = new SQLiteCommand(@"
                UPDATE Users
                SET Jmeno = @jmeno,
                    Role = @role,
                    Login = @login
                WHERE Email = @email;", conn, tx);

            updateProfileCmd.Parameters.AddWithValue("@jmeno", jmeno);
            updateProfileCmd.Parameters.AddWithValue("@email", email);
            updateProfileCmd.Parameters.AddWithValue("@role", role);
            updateProfileCmd.Parameters.AddWithValue("@login", login);
            updateProfileCmd.ExecuteNonQuery();

            if (!inserted && forceResetPassword)
            {
                using var updatePasswordCmd = new SQLiteCommand(@"
                    UPDATE Users
                    SET HesloHash = @hesloHash,
                        HesloSalt = @hesloSalt
                    WHERE Email = @email;", conn, tx);

                updatePasswordCmd.Parameters.AddWithValue("@email", email);
                updatePasswordCmd.Parameters.AddWithValue("@hesloHash", hesloHash);
                updatePasswordCmd.Parameters.AddWithValue("@hesloSalt", salt);
                updatePasswordCmd.ExecuteNonQuery();
            }
        }
    }
}