using System.Collections.Generic;
using System.Data.SQLite;
using System;
using System.Globalization;
using System.Text;
using TicketSystem.Models;

namespace TicketSystem.Data
{
    public class UserRepository
    {
        private readonly string _connStr = DatabaseHelper.ConnectionString;

        public List<User> GetAll()
        {
            var list = new List<User>();
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand("SELECT Id, Jmeno, Email, Role, Login FROM Users ORDER BY Jmeno", conn);
            using var r = cmd.ExecuteReader();

            while (r.Read())
            {
                list.Add(new User
                {
                    Id = (int)(long)r["Id"],
                    Jmeno = r["Jmeno"]?.ToString() ?? "",
                    Email = r["Email"]?.ToString() ?? "",
                    Role = r["Role"]?.ToString() ?? "User",
                    Login = r["Login"]?.ToString() ?? ""
                });
            }

            return list;
        }

        public User? Authenticate(string login, string password)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand(@"
                SELECT Id, Jmeno, Email, Role, Login, HesloHash, HesloSalt
                FROM Users
                WHERE lower(Login) = lower(@login)
                LIMIT 1;", conn);

            cmd.Parameters.AddWithValue("@login", login.Trim());

            using var r = cmd.ExecuteReader();
            if (!r.Read())
                return null;

            var storedHash = r["HesloHash"]?.ToString() ?? "";
            var storedSalt = r["HesloSalt"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(storedHash))
                return null;

            var computedHash = string.IsNullOrWhiteSpace(storedSalt)
                ? DatabaseHelper.HashPassword(password)
                : DatabaseHelper.HashPassword(password, storedSalt);

            if (!string.Equals(storedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                return null;

            return new User
            {
                Id = (int)(long)r["Id"],
                Jmeno = r["Jmeno"]?.ToString() ?? "",
                Email = r["Email"]?.ToString() ?? "",
                Role = r["Role"]?.ToString() ?? "User",
                Login = r["Login"]?.ToString() ?? ""
            };
        }

        public string GetNameById(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand("SELECT Jmeno FROM Users WHERE Id=@id LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@id", id);
            var x = cmd.ExecuteScalar()?.ToString();
            return string.IsNullOrWhiteSpace(x) ? $"User#{id}" : x;
        }

        public void CreateByAdmin(string jmeno, string heslo, string role)
        {
            var name = (jmeno ?? "").Trim();
            var password = heslo ?? "";
            var normalizedRole = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Jméno je povinné.");

            if (password.Length < 6)
                throw new ArgumentException("Heslo musí mít alespoň 6 znaků.");

            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var loginBase = BuildLoginBase(name);
            var login = GetUniqueLogin(conn, loginBase);
            var email = $"{login}@tickets.local";

            var salt = DatabaseHelper.GenerateSalt();
            var hash = DatabaseHelper.HashPassword(password, salt);

            using var cmd = new SQLiteCommand(@"
                INSERT INTO Users (Jmeno, Email, Role, Login, HesloHash, HesloSalt)
                VALUES (@jmeno, @email, @role, @login, @hash, @salt);", conn);

            cmd.Parameters.AddWithValue("@jmeno", name);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@role", normalizedRole);
            cmd.Parameters.AddWithValue("@login", login);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@salt", salt);

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (SQLiteException ex)
            {
                throw new ArgumentException("Uživatele se nepodařilo vytvořit: " + ex.Message);
            }
        }

        private static string BuildLoginBase(string input)
        {
            var text = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var ch in text)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
                else if (char.IsWhiteSpace(ch) || ch == '.' || ch == '_' || ch == '-')
                    sb.Append('.');
            }

            var login = sb.ToString().Trim('.');
            while (login.Contains("..", StringComparison.Ordinal))
                login = login.Replace("..", ".", StringComparison.Ordinal);

            return string.IsNullOrWhiteSpace(login) ? "user" : login;
        }

        private static string GetUniqueLogin(SQLiteConnection conn, string baseLogin)
        {
            var candidate = baseLogin;
            var suffix = 1;

            while (LoginExists(conn, candidate))
            {
                suffix++;
                candidate = $"{baseLogin}{suffix}";
            }

            return candidate;
        }

        private static bool LoginExists(SQLiteConnection conn, string login)
        {
            using var cmd = new SQLiteCommand("SELECT COUNT(1) FROM Users WHERE lower(Login) = lower(@login);", conn);
            cmd.Parameters.AddWithValue("@login", login);
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }
    }
}