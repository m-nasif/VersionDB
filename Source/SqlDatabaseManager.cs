using System;
using System.Data;
using System.Data.SqlClient;

namespace VersionDB
{
    /// <summary>
    /// Manages database operations for SQL Server
    /// </summary>
    public class SqlDatabaseManager
    {
        private string m_connectionString;
        private int defaultCommandTimeout = 600; //seconds
        public SqlConnection Connection;

        public SqlDatabaseManager(Database database, bool openConnection = false)
        {
            m_connectionString = database.ConnectionString;
            Connection = new SqlConnection(m_connectionString);

            if (openConnection)
            {
                OpenConnection();
            }
        }

        #region Connection

        /// <summary>
        /// Open the connection to the database
        /// </summary>
        /// <param name="connectionString">The connection string used to open the database</param>
        /// <returns>Return true if successful, otherwise false</returns>
        public void OpenConnection()
        {
            Connection.Open();
        }

        /// <summary>
        /// Close the connection to the database
        /// </summary>
        public void CloseConnection()
        {
            Connection.Close();
        }

        #endregion

        #region CreateParameter

        /// <summary>
        /// Creates a new instance of an IDbDataParameter object
        /// </summary>
        /// <param name="parameterName">The name of the parameter</param>
        /// <param name="dbType">The type of the parameter</param>
        /// <param name="value">The value of the parameter</param>
        /// <param name="size">The size of the parameter</param>
        /// <returns>Returns an IDbDataParameter object</returns>
        public IDbDataParameter CreateParameter(string parameterName, DbType dbType, Object value, int size)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = parameterName;
            param.DbType = dbType;
            param.Size = size;
            // null check has been added for sql server; it is not required in pgsql
            param.Value = (value == null) ? DBNull.Value : value;
            // allow null as param value for sql server; it is not required in pgsql
            param.IsNullable = true;
            return param;
        }

        /// <summary>
        /// Creates a new instance of an IDbDataParameter object
        /// </summary>
        /// <param name="parameterName">The name of the parameter</param>
        /// <param name="dbType">The type of the parameter</param>
        /// <param name="value">The value of the parameter</param>
        /// <returns>Returns an IDbDataParameter object</returns>
        public IDbDataParameter CreateParameter(string parameterName, DbType dbType, Object value)
        {
            SqlParameter param = new SqlParameter();
            param.ParameterName = parameterName;
            param.DbType = dbType;
            // null check has been added for sql server; it is not required in pgsql
            param.Value = (value == null) ? DBNull.Value : value;
            // allow null as param value for sql server; it is not required in pgsql
            param.IsNullable = true;
            return param;
        }

        #endregion

        #region ExecuteNonQuery

        /// <summary>
        /// Executes an SQL statement against the connection
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="transaction">The transaction within which the command object of a data provider executes</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>The number of rows affected</returns>
        public int ExecuteNonQuery(string sql, int timeout, IDbTransaction transaction, IDbDataParameter[] paramArray)
        {
            int rowsAffected = 0;
            string pattern = @"(?:^|\s)GO(?:\s|$)";
            string[] sqls = System.Text.RegularExpressions.Regex.Split(sql, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (string sqlPart in sqls)
            {
                if (!string.IsNullOrWhiteSpace(sqlPart))
                {
                    IDbCommand cmd = Connection.CreateCommand();

                    cmd.CommandText = sqlPart;
                    cmd.CommandTimeout = timeout;
                    cmd.Transaction = transaction;
                    if (paramArray != null)
                    {
                        foreach (IDbDataParameter param in paramArray)
                        {
                            cmd.Parameters.Add(param);
                        }
                    }

                    rowsAffected += cmd.ExecuteNonQuery();
                    cmd.Parameters.Clear();
                }
            }

            return rowsAffected;
        }

        /// <summary>
        /// Executes an SQL statement against the connection
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>The number of rows affected</returns>
        public int ExecuteNonQuery(string sql, int timeout, IDbDataParameter[] paramArray)
        {
            return ExecuteNonQuery(sql, timeout, null, paramArray);
        }

        /// <summary>
        /// Executes an SQL statement against the connection
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>The number of rows affected</returns>
        public int ExecuteNonQuery(string sql, IDbDataParameter[] paramArray)
        {
            return ExecuteNonQuery(sql, defaultCommandTimeout, paramArray);
        }

        public int ExecuteNonQuery(string sql, IDbTransaction transaction)
        {
            return ExecuteNonQuery(sql, defaultCommandTimeout, transaction, null);
        }

        #endregion

        #region ExecuteReader

        /// <summary>
        /// Executes an SQL statement against a connection and builds an IDataReader
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="transaction">The transaction within which the command object of a data provider executes</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return an IDataReader object</returns>
        public IDataReader ExecuteReader(string sql, int timeout, IDbTransaction transaction, params IDbDataParameter[] paramArray)
        {
            IDbCommand cmd = Connection.CreateCommand();

            //cmd.CommandTimeout = timeout;
            cmd.CommandText = sql;
            cmd.CommandTimeout = timeout;
            cmd.Transaction = transaction;

            if (paramArray != null)
            {
                foreach (IDbDataParameter param in paramArray)
                {
                    cmd.Parameters.Add(param);
                }
            }

            IDataReader reader = cmd.ExecuteReader();
            cmd.Parameters.Clear();
            return reader;
        }

        /// <summary>
        /// Executes an SQL statement against a connection and builds an IDataReader
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return an IDataReader object</returns>
        public IDataReader ExecuteReader(string sql, int timeout, params IDbDataParameter[] paramArray)
        {
            return ExecuteReader(sql, timeout, null, paramArray);
        }

        /// <summary>
        /// Executes an SQL statement against a connection and builds an IDataReader
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return an IDataReader object</returns>
        public IDataReader ExecuteReader(string sql, params IDbDataParameter[] paramArray)
        {
            return ExecuteReader(sql, defaultCommandTimeout, paramArray);
        }

        public IDataReader ExecuteReader(string sql)
        {
            return ExecuteReader(sql, defaultCommandTimeout);
        }

        #endregion

        #region GetDataSet

        /// <summary>
        /// Executes an SQL statement against a connection and builds a DataSet
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="transaction">The transaction within which the command object of a data provider executes</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return a DataSet object</returns>
        public DataSet GetDataSet(string sql, int timeout, IDbTransaction transaction, params IDbDataParameter[] paramArray)
        {
            IDbCommand cmd = Connection.CreateCommand();

            //cmd.CommandTimeout = timeout;
            cmd.CommandText = sql;
            cmd.CommandTimeout = timeout;
            cmd.Transaction = transaction;

            foreach (IDbDataParameter param in paramArray)
            {
                cmd.Parameters.Add(param);
            }

            SqlDataAdapter dataAdapter = new SqlDataAdapter();
            dataAdapter.SelectCommand = (SqlCommand)cmd;

            DataSet dataSet = new DataSet();

            dataAdapter.Fill(dataSet);
            cmd.Parameters.Clear();
            return dataSet;
        }

        /// <summary>
        /// Executes an SQL statement against a connection and builds a DataSet
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="timeout">The wait time before terminating the attempt to execute a command and generating an error</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return a DataSet object</returns>
        public DataSet GetDataSet(string sql, int timeout, params IDbDataParameter[] paramArray)
        {
            return GetDataSet(sql, timeout, null, paramArray);
        }

        /// <summary>
        /// Executes an SQL statement against a connection and builds a DataSet
        /// </summary>
        /// <param name="sql">The text command to run</param>
        /// <param name="paramArray">The parameters of the SQL statement</param>
        /// <returns>Return a DataSet object</returns>
        public DataSet GetDataSet(string sql, params IDbDataParameter[] paramArray)
        {
            return GetDataSet(sql, defaultCommandTimeout, paramArray);
        }

        #endregion
    }
}
