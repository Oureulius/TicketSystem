using System.Collections.Generic;
using System.Data.SQLite;
using System;
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
    }
}