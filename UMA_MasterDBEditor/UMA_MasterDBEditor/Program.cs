using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text.RegularExpressions;

namespace UMA_MasterDBEditor
{
    class Program
    {
        const string MASTERDB_FILE = "master.mdb";
        const string MASTERDB_PATH = "%appdata%/../LocalLow/Cygames/umamusume/master/";
        const string TRANSLATE_PATH = "csv/";
        const string CSV_REGEX = ",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
        const string CONSOLE_WRITE_FORMAT = "{0}\t{1}\t{2}\t{3}";

        private struct TranslateData
        {
            public int id;
            public int index;
            public int category;
            public string text;
        }

        /// <summary>
        /// Check CSV file
        /// </summary>
        /// <param name="filelist">found file list</param>
        /// <returns>Is exist csv</returns>
        private static bool CheckCSVFiles(out List<string> filelist)
        {
            filelist = new List<string>(); 
            string dir = Directory.GetCurrentDirectory();
            string csvdir = Path.Combine(dir, TRANSLATE_PATH);

            foreach (var node in Directory.GetFiles(dir))
            {
                if (node.Contains(".csv"))
                {
                    filelist.Add(node);
                }
            }
            if (Directory.Exists(csvdir))
            {
                foreach (var node in Directory.GetFiles(csvdir))
                {
                    if (node.Contains(".csv"))
                    {
                        filelist.Add(node);
                    }
                }
            }
            if (filelist.Count > 0) { return true; }

            return false;
        }
        /// <summary>
        /// Check MasterDB
        /// </summary>
        /// <param name="mdbpath">MasterDB path</param>
        /// <returns>Is Exist MasterDB</returns>
        private static bool CheckMDBFile(out string mdbpath)
        {
            string dir = Directory.GetCurrentDirectory();
            if(File.Exists(Path.Combine(dir, MASTERDB_FILE)))
            {
                mdbpath = Path.Combine(dir, MASTERDB_FILE);
                return true;
            }
            else if(File.Exists(Path.Combine(MASTERDB_PATH, MASTERDB_FILE)))
            {
                mdbpath = Path.Combine(MASTERDB_PATH, MASTERDB_FILE);
                return true;
            }
            else
            {
                mdbpath = null;
                return false;
            }
        }

        static void Main(string[] args)
        {
            if (CheckCSVFiles(out List<string> fileList) == false)
            {
                Console.WriteLine("Can`t open CSV");
            }
            else if (CheckMDBFile(out string mdbPath) == false)
            {
                Console.WriteLine("Can`t open master.mdb");
            }
            else
            {
                ///csv parser
                List<TranslateData> translist = new List<TranslateData>();
                foreach (string csvfile in fileList)
                {
                    using (StreamReader streamReader = new StreamReader(csvfile))
                    {
                        if (streamReader.EndOfStream == false) { streamReader.ReadLine(); }
                        while (streamReader.EndOfStream == false)
                        {
                            string readline = streamReader.ReadLine();
                            Regex regex = new Regex(CSV_REGEX);
                            string[] splitline = regex.Split(readline);
                            if (splitline.Length - 1 < 5) { continue; }

                            splitline[4] = splitline[4].Trim('\"');
                            if (splitline[4] == string.Empty || splitline[4].Trim() == "") { continue; }

                            TranslateData data = new TranslateData()
                            {
                                id = int.Parse(splitline[0]),
                                category = int.Parse(splitline[1]),
                                index = int.Parse(splitline[2]),
                                text = splitline[4]
                            };
                            translist.Add(data);
                        }

                        streamReader.Close();
                    }
                }

                ///open sqlite3 master db
                using (SQLiteConnection connection = new SQLiteConnection("Data Source=" + mdbPath))
                {
                    connection.Open();

                    ///update master db text_data
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        foreach (var node in translist)
                        {
                            using (SQLiteCommand cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = "UPDATE text_data SET text = @TEXT WHERE id=@ID AND category=@CAT AND \"index\"=@IDX";
                                cmd.Parameters.Add(new SQLiteParameter("@ID", node.id));
                                cmd.Parameters.Add(new SQLiteParameter("@IDX", node.index));
                                cmd.Parameters.Add(new SQLiteParameter("@CAT", node.category));
                                cmd.Parameters.Add(new SQLiteParameter("@TEXT", node.text));
                                cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }

                    ///check master db text_data
                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        foreach (var node in translist)
                        {
                            command.CommandText = "SELECT * FROM text_data WHERE id=@ID AND category=@CAT AND \"index\"=@IDX";
                            command.Parameters.Add(new SQLiteParameter("@ID", node.id));
                            command.Parameters.Add(new SQLiteParameter("@IDX", node.index));
                            command.Parameters.Add(new SQLiteParameter("@CAT", node.category));

                            using (SQLiteDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    Console.WriteLine(string.Format(CONSOLE_WRITE_FORMAT, reader["id"], reader["category"], reader["index"], reader["text"]));
                                }

                            }
                        }
                    }

                    ///sqlite3 connection close
                    connection.Close();
                }
            }

            Console.ReadKey();
        }
    }
}
