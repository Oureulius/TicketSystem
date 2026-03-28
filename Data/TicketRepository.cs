using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using TicketSystem.Models;

namespace TicketSystem.Data
{
    public class TicketRepository
    {
        private readonly string _connStr = DatabaseHelper.ConnectionString;

        public List<Ticket> GetAll()
        {
            var list = new List<Ticket>();

            using var conn = new SQLiteConnection(_connStr);
            conn.Open();

            using var fk = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn);
            fk.ExecuteNonQuery();

            var cmd = new SQLiteCommand("SELECT * FROM Tickets ORDER BY datetime(Vytvoreno) DESC", conn);
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
    }
}