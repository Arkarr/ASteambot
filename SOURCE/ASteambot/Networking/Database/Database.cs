
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Networking
{
    public class Database
    {
        private string connectionString;
        
        public bool IsConnected { get; private set; }

        public Database(string server, string username, string password, string database, string port = "3306")
        {
            connectionString = "Server=" + server + ";" + "Port=" + port + ";Uid=" + username + ";Pwd=" + password + ";CharSet = utf8;";

            try
            {
                Console.WriteLine("Connecting to database server...");

                MySqlConnection connection = new MySqlConnection(connectionString);
                connection.Open();
                Console.WriteLine("Success.");

                Console.WriteLine("Attempt to connect to database...");

                QUERY("CREATE DATABASE IF NOT EXISTS `" + database + "` DEFAULT CHARSET=utf8");

                connection.Close();
                connectionString += "Database = " + database;

                Console.WriteLine("Connection to database successfully done !");
                IsConnected = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection failed ! (" + ex + ")");
                IsConnected = false;
            }
        }

        public string BuildQuery(string type, string table, string[] rows, string[] values, string args = "")
        {
            if (type == "INSERT")
            {
                string request = type + " INTO " + table + " (";

                for (int i = 0; i < rows.Length; i++)
                {
                    request += "`" + rows[i] + "`";
                    if (i != rows.Length - 1)
                        request += ", ";
                }

                request += ") VALUES (";

                for (int i = 0; i < values.Length; i++)
                {
                    request += "\"" + values[i] + "\"";
                    if (i != values.Length - 1)
                        request += ", ";
                }
                request += ");";

                return request;
            }

            return "UNKNOW QUERY TYPE :" + type;
        }

        public List<Dictionary<string, string>> SELECT(string[] rows, string table, string args = "")
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();

            MySqlCommand command;
            MySqlDataReader dataReader;
            int nbrItems = rows.Length;
            string request = "SELECT ";

            for (int i = 0; i < nbrItems; i++)
            {
                request += rows[i];
                if (i != nbrItems - 1)
                    request += ", ";
            }

            request += " FROM " + table;

            if (args != "")
            {
                request += " " + args;
            }

            request += ";";

            List<Dictionary<string, string>> data = new List<Dictionary<string, string>>();

            command = new MySqlCommand(request, connection);
            dataReader = command.ExecuteReader(); //TODO
            while (dataReader.Read())
            {
                Dictionary<string, string> track = new Dictionary<string, string>();
                for (int i = 0; i < nbrItems; i++)
                    track.Add(rows[i], dataReader.GetValue(i).ToString());
                data.Add(track);
            }
            dataReader.Close();
            command.Dispose();
            connection.Close();
            
            return data;
        }

        public bool INSERT(string table, string[] rows, string[] values)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();


            if (rows.Length != values.Length)
            {
                if (rows.Length > values.Length)
                {
                    Console.WriteLine("There is more cols then values to add !");
                    return false;
                }

                if (rows.Length < values.Length)
                {
                    Console.WriteLine("There is more values then cols to add !");
                    return false;
                }
            }

            MySqlCommand command;
            string request = "INSERT INTO " + table + " (";

            for (int i = 0; i < rows.Length; i++)
            {
                request += "`" + rows[i] + "`";
                if (i != rows.Length - 1)
                    request += ", ";
            }

            request += ") VALUES (";

            for (int i = 0; i < values.Length; i++)
            {
                request += "\"" + values[i] + "\"";
                if (i != values.Length - 1)
                    request += ", ";
            }
            request += ");";

            command = new MySqlCommand(request, connection);
            command.ExecuteNonQuery();
            command.Dispose();
            connection.Close();
            
            return true;
        }

        public bool DELETE(string rows, string values, string table)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();

            MySqlCommand command;
            int nbrItems = rows.Length;
            string request = "DELETE FROM " + table + " WHERE `" + rows + "`=\"" + values + "\"";

            command = new MySqlCommand(request, connection);
            command.ExecuteNonQuery();
            command.Dispose();
            connection.Close();
            
            return true;
        }

        public bool QUERY(string query)
        {
            MySqlConnection connection = new MySqlConnection(connectionString);
            connection.Open();


            MySqlCommand command;
            command = new MySqlCommand(query, connection);
            command.ExecuteNonQuery();
            command.Dispose();
            connection.Close();
            
            return true;
        }

        public void InitialiseDatabase()
        {
            try
            {
                QUERY("DROP TABLE IF EXISTS tradeoffers");

                string table1 = "CREATE TABLE IF NOT EXISTS `tradeoffer` (" +
                                "`ID` int(11) NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                                "`steamID` bigint(255) NOT NULL," +
                                "`tradeOfferID` bigint(255) NOT NULL," +
                                "`tradeValue` float(50) NOT NULL," +
                                "`tradeStatus` varchar(50) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8";
                QUERY(table1);

                string table2 = "CREATE TABLE IF NOT EXISTS `smitems` (" +
                                 "`ID` int(11) NOT NULL AUTO_INCREMENT PRIMARY KEY," +
                                 "`itemName` varchar(125) CHARACTER SET utf8 NOT NULL," +
                                 "`last_updated` varchar(125) NOT NULL," +
                                 "`value` float(15) NOT NULL," +
                                 "`quantity` int(15) NOT NULL," +
                                 "`gameid` int(15) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8";
                QUERY(table2);

                string table3 = "CREATE TABLE IF NOT EXISTS `tradeusertoken` (" +
                                 "`steamID` varchar(125) NOT NULL PRIMARY KEY," +
                                 "`tradetoken` varchar(125) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8";
                QUERY(table3); 
            }
            catch(MySqlException e)
            {
                if(e.Message.EndsWith("'utf8'"))
                {
                    Program.PrintErrorMessage("Couldn't find character set \"utf8mb4\" !");
                    throw new Exception("UPDATE YOUR MySQL SERVER TO AT LEAST 5.5.3 !");
                }                
            }
        }
    }
}
