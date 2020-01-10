// Copyright � 2004, 2018, Oracle and/or its affiliates. All rights reserved.
//
// MySQL Connector/NET is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most 
// MySQL Connectors. There are special exceptions to the terms and 
// conditions of the GPLv2 as it is applied to this software, see the 
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify 
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License 
// for more details.
//
// You should have received a copy of the GNU General Public License along 
// with this program; if not, write to the Free Software Foundation, Inc., 
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using System;
#if !RT
using System.Data;
using System.Data.Common;
#endif
using System.Collections;
using MySql.Data.Types;
using System.Collections.Generic;
using System.Globalization;
using MySql.Data.Common;
using MySql.Data.MySqlClient;
using System.Threading;
using MCGalaxy.SQL;

namespace MySql.Data.MySqlClient
{
  public sealed partial class MySqlDataReader : IDBDataReader
  {
    // The DataReader should always be open when returned to the user.
    private bool isOpen = true;

    private CommandBehavior commandBehavior;
    private MySqlCommand command;
    internal long affectedRows;
    internal Driver driver;
    private PreparableStatement statement;
    private ResultSet resultSet;
    private bool disposed = false;

    /* 
     * Keep track of the connection in order to implement the
     * CommandBehavior.CloseConnection flag. A null reference means
     * normal behavior (do not automatically close).
     */
    private MySqlConnection connection;

    /*
     * Because the user should not be able to directly create a 
     * DataReader object, the constructors are
     * marked as internal.
     */
    internal MySqlDataReader(MySqlCommand cmd, PreparableStatement statement)
    {
      this.command = cmd;
      connection = (MySqlConnection)command.Connection;
      driver = connection.driver;
      affectedRows = -1;
      this.statement = statement;
    }

    #region Properties

    internal PreparableStatement Statement
    {
      get { return statement; }
    }

    internal MySqlCommand Command
    {
      get { return command; }
    }

    internal ResultSet ResultSet
    {
      get { return resultSet; }
    }

    public int FieldCount
    {
      get { return resultSet == null ? 0 : resultSet.Size; }
    }

    public bool IsClosed
    {
      get { return !isOpen; }
    }

    public int RecordsAffected
    {
      // RecordsAffected returns the number of rows affected in batch
      // statments from insert/delete/update statments.  This property
      // is not completely accurate until .Close() has been called.
      get { return (int)affectedRows; }
    }

    #endregion

    /// <summary>
    /// Closes the MySqlDataReader object.
    /// </summary>
    public void Close()
    {
      if (!isOpen) return;

      bool shouldCloseConnection = (commandBehavior & CommandBehavior.CloseConnection) != 0;
      CommandBehavior originalBehavior = commandBehavior;

      // clear all remaining resultsets
      try
      {
        // Temporarily change to Default behavior to allow NextResult to finish properly.
        if (!originalBehavior.Equals(CommandBehavior.SchemaOnly))
          commandBehavior = CommandBehavior.Default;
        while (NextResult()) { }
      }
      catch (MySqlException ex)
      {
        // Ignore aborted queries
        if (!ex.IsQueryAborted)
        {
          // ignore IO exceptions.
          // We are closing or disposing reader, and  do not
          // want exception to be propagated to used. If socket is
          // is closed on the server side, next query will run into
          // IO exception. If reader is closed by GC, we also would 
          // like to avoid any exception here. 
          bool isIOException = false;
          for (Exception exception = ex; exception != null;
            exception = exception.InnerException)
          {
            if (exception is System.IO.IOException)
            {
              isIOException = true;
              break;
            }
          }
          if (!isIOException)
          {
            // Ordinary exception (neither IO nor query aborted)
            throw;
          }
        }
      }
      catch (System.IO.IOException)
      {
        // eat, on the same reason we eat IO exceptions wrapped into 
        // MySqlExceptions reasons, described above.
      }
      finally
      {
        // always ensure internal reader is null (Bug #55558)
        connection.Reader = null;
        commandBehavior = originalBehavior;
      }
      // we now give the command a chance to terminate.  In the case of
      // stored procedures it needs to update out and inout parameters
      command.Close(this);
      commandBehavior = CommandBehavior.Default;

      if (shouldCloseConnection)
        connection.Close();

      command = null;
      connection.IsInUse = false;
      connection = null;
      isOpen = false;
    }

    #region TypeSafe Accessors

    public bool GetBoolean(int i)
    {
      return Convert.ToBoolean(GetValue(i));
    }

    public byte GetByte(int i)
    {
      IMySqlValue v = GetFieldValue(i, false);
      if (v is MySqlUByte)
        return ((MySqlUByte)v).Value;
      else
        return (byte)((MySqlByte)v).Value;
    }
    
    public sbyte GetSByte(int i)
    {
      IMySqlValue v = GetFieldValue(i, false);
      if (v is MySqlByte)
        return ((MySqlByte)v).Value;
      else
        return (sbyte)((MySqlByte)v).Value;
    }

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
    {
      if (i >= FieldCount)
        throw new IndexOutOfRangeException();

      IMySqlValue val = GetFieldValue(i, false);

      if (!(val is MySqlBinary))
        throw new MySqlException("GetBytes can only be called on binary columns");

      byte[] bytes = ((MySqlBinary)val).Value;
      if (buffer == null)
        return bytes.Length;

      if (bufferoffset >= buffer.Length || bufferoffset < 0)
        throw new IndexOutOfRangeException("Buffer index must be a valid index in buffer");
      if (buffer.Length < (bufferoffset + length))
        throw new ArgumentException("Buffer is not large enough to hold the requested data");
      if (fieldOffset < 0 ||
        ((ulong)fieldOffset >= (ulong)bytes.Length && (ulong)bytes.Length > 0))
        throw new IndexOutOfRangeException("Data index must be a valid index in the field");

      // adjust the length so we don't run off the end
      if ((ulong)bytes.Length < (ulong)(fieldOffset + length))
      {
        length = (int)((ulong)bytes.Length - (ulong)fieldOffset);
      }

      Buffer.BlockCopy(bytes, (int)fieldOffset, buffer, (int)bufferoffset, (int)length);

      return length;
    }

    private object ChangeType(IMySqlValue value, int fieldIndex, Type newType)
    {
#if !RT
      resultSet.Fields[fieldIndex].AddTypeConversion(newType);
#endif
      return Convert.ChangeType(value.Value, newType, CultureInfo.InvariantCulture);
    }

    public char GetChar(int i)
    {
      string s = GetString(i);
      return s[0];
    }

    public String GetDataTypeName(int i)
    {
      if (!isOpen)
        throw new Exception("No current query in data reader");
      if (i >= FieldCount)
        throw new IndexOutOfRangeException();

      // return the name of the type used on the backend
      IMySqlValue v = resultSet.Values[i];
      return v.MySqlTypeName;
    }

    public DateTime GetDateTime(int i)
    {
      IMySqlValue val = GetFieldValue(i, true);
      MySqlDateTime dt;

      if (val is MySqlDateTime)
        dt = (MySqlDateTime)val;
      else
      {
        // we need to do this because functions like date_add return string
        string s = GetString(i);
        dt = MySqlDateTime.Parse(s);
      }

      dt.TimezoneOffset = driver.timeZoneOffset;
      if (connection.Settings.ConvertZeroDateTime && !dt.IsValidDateTime)
        return DateTime.MinValue;
      else
        return dt.GetDateTime();
    }

    public Decimal GetDecimal(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlDecimal)
        return ((MySqlDecimal)v).Value;
      return Convert.ToDecimal(v.Value);
    }

    public double GetDouble(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlDouble)
        return ((MySqlDouble)v).Value;
      return Convert.ToDouble(v.Value);
    }

    public Type GetFieldType(int i)
    {
      if (!isOpen)
        throw new Exception("No current query in data reader");
      if (i >= FieldCount)
        throw new IndexOutOfRangeException();

      // we have to use the values array directly because we can't go through
      // GetValue
      IMySqlValue v = resultSet.Values[i];
      if (v is MySqlDateTime)
      {
        if (!connection.Settings.AllowZeroDateTime)
          return typeof(DateTime);
        return typeof(MySqlDateTime);
      }
      return v.SystemType;
    }

    public  float GetFloat(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlSingle)
        return ((MySqlSingle)v).Value;
      return Convert.ToSingle(v.Value);
    }

    public Int16 GetInt16(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlInt16)
        return ((MySqlInt16)v).Value;

      return (short)ChangeType(v, i, typeof(short));
    }

    public Int32 GetInt32(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlInt32)
        return ((MySqlInt32)v).Value;

      return (Int32)ChangeType(v, i, typeof(Int32));
    }

    public Int64 GetInt64(int i)
    {
      IMySqlValue v = GetFieldValue(i, true);
      if (v is MySqlInt64)
        return ((MySqlInt64)v).Value;

      return (Int64)ChangeType(v, i, typeof(Int64));
    }

    public string GetName(int i)
    {
      if (!isOpen)
        throw new Exception("No current query in data reader");
      if (i >= FieldCount)
        throw new IndexOutOfRangeException();

      return resultSet.Fields[i].ColumnName;
    }

    public int GetOrdinal(string name)
    {
      if (!isOpen || resultSet == null)
        throw new Exception("No current query in data reader");

      return resultSet.GetOrdinal(name);
    }

    public string GetString(int i)
    {
      IMySqlValue val = GetFieldValue(i, true);

      if (val is MySqlBinary)
      {
        byte[] v = ((MySqlBinary)val).Value;
        return resultSet.Fields[i].Encoding.GetString(v, 0, v.Length);
      }

      return val.Value.ToString();
    }

    public object GetValue(int i)
    {
      if (!isOpen)
        throw new Exception("No current query in data reader");
      if (i >= FieldCount)
        throw new IndexOutOfRangeException();

      IMySqlValue val = GetFieldValue(i, false);
      if (val.IsNull)
        return DBNull.Value;

      // if the column is a date/time, then we return a MySqlDateTime
      // so .ToString() will print '0000-00-00' correctly
      if (val is MySqlDateTime)
      {
        MySqlDateTime dt = (MySqlDateTime)val;
        if (!dt.IsValidDateTime && connection.Settings.ConvertZeroDateTime)
          return DateTime.MinValue;
        else if (connection.Settings.AllowZeroDateTime)
          return val;
        else
          return dt.GetDateTime();
      }

      return val.Value;
    }

    public int GetValues(object[] values)
    {
      int numCols = Math.Min(values.Length, FieldCount);
      for (int i = 0; i < numCols; i++)
        values[i] = GetValue(i);

      return numCols;
    }

    public UInt16 GetUInt16(int column)
    {
      IMySqlValue v = GetFieldValue(column, true);
      if (v is MySqlUInt16)
        return ((MySqlUInt16)v).Value;

      return (UInt16)ChangeType(v, column, typeof(UInt16));
    }

    public UInt32 GetUInt32(int column)
    {
      IMySqlValue v = GetFieldValue(column, true);
      if (v is MySqlUInt32)
        return ((MySqlUInt32)v).Value;
      return (uint)ChangeType(v, column, typeof(UInt32));
    }

    public UInt64 GetUInt64(int column)
    {
      IMySqlValue v = GetFieldValue(column, true);
      if (v is MySqlUInt64)
        return ((MySqlUInt64)v).Value;

      return (UInt64)ChangeType(v, column, typeof(UInt64));
    }


    #endregion

    public bool IsDBNull(int i)
    {
      return DBNull.Value == GetValue(i);
    }

    public bool NextResult()
    {
      if (!isOpen)
        throw new MySqlException("Invalid attempt to call NextResult when the reader is closed");

      // this will clear out any unread data
      if (resultSet != null)
      {
        resultSet.Close();
      }

      // single result means we only return a single resultset.  If we have already
      // returned one, then we return false
      // TableDirect is basically a select * from a single table so it will generate
      // a single result also
      if (resultSet != null &&
        ((commandBehavior & CommandBehavior.SingleResult) != 0))
        return false;

      // next load up the next resultset if any
      try
      {
        do
        {
          resultSet = driver.NextResult(Statement.StatementId, false);
          if (resultSet == null) return false;

          if (resultSet.Size == 0)
          {
            Command.lastInsertedId = resultSet.InsertedId;
            if (affectedRows == -1)
              affectedRows = resultSet.AffectedRows;
            else
              affectedRows += resultSet.AffectedRows;
          }
        } while (resultSet.Size == 0);

        return true;
      }
      catch (MySqlException ex)
      {
        if (ex.IsFatal)
          connection.Abort();
        if (ex.Number == 0)
          throw new MySqlException("Fatal error encountered attempting to read the resultset", ex);
        if ((commandBehavior & CommandBehavior.CloseConnection) != 0)
          Close();
        throw;
      }
    }

    public bool Read()
    {
      if (!isOpen)
        throw new MySqlException("Invalid attempt to Read when reader is closed.");
      if (resultSet == null)
        return false;

      try
      {
        return resultSet.NextRow(commandBehavior);
      }
      catch (TimeoutException tex)
      {
        connection.HandleTimeoutOrThreadAbort(tex);
        throw; // unreached
      }
      catch (ThreadAbortException taex)
      {
        connection.HandleTimeoutOrThreadAbort(taex);
        throw;
      }
      catch (MySqlException ex)
      {
        if (ex.IsFatal)
          connection.Abort();

        if (ex.IsQueryAborted)
        {
          throw;
        }

        throw new MySqlException("Fatal error encountered during data read", ex);
      }
    }


    private IMySqlValue GetFieldValue(int index, bool checkNull)
    {
      if (index < 0 || index >= FieldCount)
        throw new ArgumentException("You have specified an invalid column ordinal");

      IMySqlValue v = resultSet[index];

      if (checkNull && v.IsNull)
        throw new System.Data.SqlTypes.SqlNullValueException();

      return v;
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
      if (disposed) return;
      if (disposing) Close();
      disposed = true;
    }
    
    ~MySqlDataReader() { Dispose(false); }
  }
}
