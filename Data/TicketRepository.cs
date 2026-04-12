using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using TicketSystem.Models;
using System.Linq;

namespace TicketSystem.Data
{
    public class TicketRepository
    {
        private readonly string _connStr = DatabaseHelper.ConnectionString;

        public List<Ticket> GetAll()
            => GetFiltered(null, null, null);

        public List<Ticket> GetFiltered(string? priorita, string? kategorie, int? vytvorenoUzivatelem)
        {
            var list = new List<Ticket>();

            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            using var fk = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn);
            fk.ExecuteNonQuery();

            var sql = new StringBuilder(@"
                SELECT Id, Nadpis, Popisek, Status, Priorita, Kategorie, Vytvoreno, ZmenaCasu, VytvorenoUzivatelem, PridelenoUzivatelem
                FROM Tickets
                WHERE 1=1");

            using var cmd = new SQLiteCommand(conn);

            if (!string.IsNullOrWhiteSpace(priorita))
            {
                sql.Append(" AND Priorita = @priorita");
                cmd.Parameters.AddWithValue("@priorita", priorita);
            }

            if (!string.IsNullOrWhiteSpace(kategorie))
            {
                sql.Append(" AND Kategorie = @kategorie");
                cmd.Parameters.AddWithValue("@kategorie", kategorie);
            }

            if (vytvorenoUzivatelem.HasValue)
            {
                sql.Append(" AND VytvorenoUzivatelem = @vytvorenoUzivatelem");
                cmd.Parameters.AddWithValue("@vytvorenoUzivatelem", vytvorenoUzivatelem.Value);
            }

            sql.Append(" ORDER BY datetime(Vytvoreno) DESC");
            cmd.CommandText = sql.ToString();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Ticket
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Nadpis = reader["Nadpis"]?.ToString() ?? "",
                    Popisek = reader["Popisek"]?.ToString() ?? "",
                    Status = reader["Status"]?.ToString() ?? "Otevřený",
                    Priorita = reader["Priorita"]?.ToString() ?? "Střední",
                    Kategorie = reader["Kategorie"]?.ToString() ?? "",
                    Vytvoreno = DateTime.TryParse(reader["Vytvoreno"]?.ToString(), out var dt) ? dt : DateTime.Now,
                    ZmenaCasu = DateTime.TryParse(reader["ZmenaCasu"]?.ToString(), out var zt) ? zt : null,
                    VytvorenoUzivatelem = Convert.ToInt32(reader["VytvorenoUzivatelem"]),
                    PridelenoUzivatelem = reader["PridelenoUzivatelem"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(reader["PridelenoUzivatelem"])
                });
            }

            return list;
        }

        public void Insert(Ticket t)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            using var fk = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn);
            fk.ExecuteNonQuery();

            var cmd = new SQLiteCommand(@"
                INSERT INTO Tickets
                    (Nadpis, Popisek, Status, Priorita, Kategorie, VytvorenoUzivatelem, PridelenoUzivatelem)
                VALUES
                    (@nadpis, @popisek, @status, @priorita, @kategorie, @vytvorenoUzivatelem, @pridelenoUzivatelem)", conn);

            cmd.Parameters.AddWithValue("@nadpis", t.Nadpis);
            cmd.Parameters.AddWithValue("@popisek", t.Popisek ?? "");
            cmd.Parameters.AddWithValue("@status", t.Status ?? "Otevřený");
            cmd.Parameters.AddWithValue("@priorita", t.Priorita ?? "Střední");
            cmd.Parameters.AddWithValue("@kategorie", t.Kategorie ?? "");
            cmd.Parameters.AddWithValue("@vytvorenoUzivatelem", t.VytvorenoUzivatelem <= 0 ? 1 : t.VytvorenoUzivatelem);

            if (t.PridelenoUzivatelem.HasValue)
                cmd.Parameters.AddWithValue("@pridelenoUzivatelem", t.PridelenoUzivatelem.Value);
            else
                cmd.Parameters.AddWithValue("@pridelenoUzivatelem", DBNull.Value);

            cmd.ExecuteNonQuery();
        }

        public void CloseTicket(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            var cmd = new SQLiteCommand(@"
                UPDATE Tickets
                SET Status = 'Uzavřený',
                    ZmenaCasu = datetime('now')
                WHERE Id = @id", conn);

            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public void Delete(int id)
        {
            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Tickets WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }

        public List<string> GetDistinctPriorities()
        {
            const string sql = @"
        SELECT DISTINCT Priorita
        FROM Tickets
        WHERE Priorita IS NOT NULL AND trim(Priorita) <> ''
        ORDER BY Priorita COLLATE NOCASE;";
            return GetDistinctStringValues(sql);
        }

        public List<string> GetDistinctCategories()
        {
            const string sql = @"
        SELECT DISTINCT Kategorie
        FROM Tickets
        WHERE Kategorie IS NOT NULL AND trim(Kategorie) <> ''
        ORDER BY Kategorie COLLATE NOCASE;";
            return GetDistinctStringValues(sql);
        }

        private List<string> GetDistinctStringValues(string sql)
        {
            var list = new List<string>();

            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            using var cmd = new SQLiteCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var value = reader[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    list.Add(value);
            }

            return list;
        }
    }
}