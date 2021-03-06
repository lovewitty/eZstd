using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace eZstd.Data
{
    /// <summary>
    /// 利用ADO.NET连接Excel数据库，并执行相应的操作：
    /// 创建表格，读取数据，写入数据，获取工作簿中的所有工作表名称。
    /// </summary>
    public static class ExcelDbHelper
    {
        #region Excel 数据库的连接

        /// <summary>
        /// 创建对Excel工作簿的连接
        /// </summary>
        /// <param name="excelWorkbookPath">要进行连接的Excel工作簿的路径</param>
        /// <param name="iMEX"> 数据库的连接方式。
        /// 	当 IMEX=0 时为“导出模式Export mode”，这个模式开启的 Excel 档案只能用来做“写入”用途；
        ///  	当 IMEX=1 时为“导入模式Import mode”，这个模式开启的 Excel 档案只能用来做“读取”用途。IMEX=1将前8行的值中有字符类型的字段的数据类型看作字符类型；
        /// 	当 IMEX=2 时为“连结模式Linked mode (full update capabilities)”，这个模式开启的 Excel 档案可同时支持“读取”与“写入”用途。</param>
        /// <returns>一个OleDataBase的Connection连接，此连接还没有Open。</returns>
        /// <remarks></remarks>
        public static OleDbConnection ConnectToExcel(string excelWorkbookPath, byte iMEX)
        {
            string strConn = string.Empty;
            if (excelWorkbookPath.EndsWith(".xls"))
            {
                strConn = "Provider=Microsoft.Jet.OLEDB.4.0; " +
                          "Data Source=" + excelWorkbookPath + "; " +
                          $"Extended Properties='Excel 8.0;HDR=YES;IMEX={iMEX}'";
            }

            else if (excelWorkbookPath.EndsWith(".xlsx") || excelWorkbookPath.EndsWith(".xlsb"))
            {
                strConn = "Provider=Microsoft.ACE.OLEDB.12.0;" +
                          "Data Source=" + excelWorkbookPath + ";" +
                          $"Extended Properties='Excel 12.0;HDR=YES;IMEX={iMEX}'";
            }

            OleDbConnection conn = new OleDbConnection(strConn);
            return conn;
        }

        /// <summary> 打开 Excel 数据库 </summary>
        /// <param name="excelConnection"></param>
        /// <returns>如果打开成功，则返回 true</returns>
        public static bool OpenConnection(OleDbConnection excelConnection)
        {
            if (excelConnection.State != ConnectionState.Open)
            {
                try
                {
                    excelConnection.Open();
                    return true;
                }
                catch (Exception ex)
                {
                    // 出错类型为 AccessViolationException. 

                    // 请检查是否安装 AccessDatabaseEngine 2007 或 AccessDatabaseEngine 2010 X64。推荐后者。
                    MessageBox.Show(ex.Message + @"请检查是否安装 AccessDatabaseEngine 2007 或 AccessDatabaseEngine 2010 X64。",
                        @"打开Excel数据库出错", MessageBoxButtons.OK);

                    // 另外，可以尝试将数据库连接字符串修改为：
                    //strConn = "Provider=Microsoft.ACE.OLEDB.12.0;" +
                    //          "Data Source=" + excelWorkbookPath + ";" +
                    //          "OLE DB Services=-1;" +
                    //          $"Extended Properties='Excel 12.0;HDR=YES;IMEX={iMEX}'";

                    return false;
                }
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 验证连接的数据源是否是Excel数据库
        /// </summary>
        /// <param name="excelWorkbookPath"> Excel 工作簿的绝对路径 </param>
        /// <returns>如果是Excel数据库，则返回True，否则返回False。</returns>
        /// <remarks></remarks>
        public static bool IsExcelDataSource(string excelWorkbookPath)
        {
            //考察连接是否是针对于Excel文档的。
            //"C:\Users\Administrator\Desktop\测试Visio与Excel交互\数据.xlsx"
            var strExt = Path.GetExtension(excelWorkbookPath);
            if (string.IsNullOrEmpty(strExt))
            {
                return false;
            }
            strExt = strExt.TrimEnd();
            return string.Compare(strExt, ".xlsx", StringComparison.OrdinalIgnoreCase) == 0
                   || string.Compare(strExt, ".xls", StringComparison.OrdinalIgnoreCase) == 0
                   || string.Compare(strExt, ".xlsb", StringComparison.OrdinalIgnoreCase) == 0;
        }

        #endregion

        #region Excel数据库元数据的获取

        /// <summary>
        /// 从对于Excel的数据连接中获取Excel工作簿中的所有工作表（不包含Excel中的命名区域NamedRange）
        /// </summary>
        /// <param name="conn"></param>
        /// <returns>集合中所有工作表的名称都带有后缀 $ 。如果此连接不是连接到Excel数据库，则返回Nothing</returns>
        /// <remarks></remarks>
        public static List<string> GetSheetsName(OleDbConnection conn)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                //获取工作簿连接中的每一个工作表，
                //注意下面的Rows属性返回的并不是Excel工作表中的每一行，而是Excel工作簿中的所有工作表。
                DataRowCollection Tables = conn.GetSchema("Tables").Rows;
                //
                List<string> sheetNames = new List<string>();
                for (int i = 0; i <= Tables.Count - 1; i++)
                {
                    //注意这里的表格Table是以DataRow的形式出现的。
                    string name = Tables[i]["TABLE_NAME"].ToString();
                    if (name.EndsWith("$"))
                    {
                        // 如果是一般的工作表，其返回的工作表名中会以$作为后缀，而Excel中的命名区域也是一种表，但是其表名不含有后缀“$”
                        sheetNames.Add(name);
                    }
                    else if (name.StartsWith("'") && name.EndsWith("$'"))
                    {
                        // 对于Excel工作表中的“TX-TCX8”，通过ADO所得到的工作表名为“'TX-TCX8$'”（注意两边的间引号），
                        // 但是在通过ADO进行数据提取时，使用的工作表名为“TX-TCX8$”。
                        sheetNames.Add(name.Substring(1, name.Length - 2));
                    }
                }
                return sheetNames;
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// 获取指定工作表中所有字段的名称，包括主键
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"> 要在哪一个工作表中提取字段信息，表名的格式为“Sheet1$”</param>
        /// <remarks></remarks>
        public static IList<string> GetFieldNames(OleDbConnection conn, string tableName)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            var dt = conn.GetSchema("Columns", new string[] { null, null, tableName });
            var names = DataTableHelper.GetValue(dt, "COLUMN_NAME");
            return names.AsEnumerable().Select(r => r.ToString()).ToList(); ;
        }

        /// <summary>
        /// 获取指定工作表中所有字段的数据类型。
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableName"> 要在哪一个工作表中提取字段信息，表名的格式为“Sheet1$”</param>
        /// <remarks>Excel中字段的数据类型是以数字来表示的，其中：时间=7；</remarks>
        public static IList<string> GetFieldDataType(OleDbConnection conn, string tableName)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            var dt = conn.GetSchema("Columns", new string[] { null, null, tableName });
            MessageBox.Show(dt.Rows.Count.ToString());
            var names = DataTableHelper.GetValue(dt, "DATA_TYPE");
            return names.AsEnumerable().Select(r => r.ToString()).ToList();
        }

        #endregion

        #region 提取Excel中的数据记录

        /// <summary>
        /// 读取Excel整张工作表中的所有字段的数据
        /// </summary>
        /// <param name="conn">OleDB的数据连接</param>
        /// <param name="sheetName">要读取的数据所在的工作表，名称中请自行包括后缀$</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static DataTable GetDataFromSheet(OleDbConnection conn, string sheetName)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                //创建向数据库发出的指令
                OleDbCommand olecmd = conn.CreateCommand();
                //类似SQL的查询语句这个[Sheet1$对应Excel文件中的一个工作表]
                //如果要提取Excel中的工作表中的某一个指定区域的数据，可以用："select * from [Sheet3$A1:C5]"
                olecmd.CommandText = "select * from [" + sheetName + "]";

                //创建数据适配器——根据指定的数据库指令
                OleDbDataAdapter Adapter = new OleDbDataAdapter(olecmd);

                //创建一个数据集以保存数据
                DataSet dtSet = new DataSet();

                //将数据适配器按指令操作的数据填充到数据集中的某一工作表中（默认为“Table”工作表）
                Adapter.Fill(dtSet);

                //索引数据集中的第一个工作表对象
                DataTable dataTable = dtSet.Tables[0]; // conn.GetSchema("Tables")

                return dataTable;
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// 读取Excel工作表中的某一个字段数据
        /// </summary>
        /// <param name="conn">OleDB的数据连接</param>
        /// <param name="SheetName">要读取的数据所在的工作表，名称中请自行包括后缀$</param>
        /// <param name="FieldName">在读取的字段</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string[] GetFieldDataFromExcel(OleDbConnection conn, string SheetName, string FieldName)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                //创建向数据库发出的指令
                OleDbCommand olecmd = conn.CreateCommand();
                //类似SQL的查询语句这个[Sheet1$对应Excel文件中的一个工作表]
                //如果要提取Excel中的工作表中的某一个指定区域的数据，可以用："select * from [Sheet3$A1:C5]"
                olecmd.CommandText = $"select [{FieldName}] from [{SheetName}]";

                //创建数据适配器——根据指定的数据库指令
                OleDbDataAdapter Adapter = new OleDbDataAdapter(olecmd);

                //创建一个数据集以保存数据
                DataSet dtSet = new DataSet();

                //将数据适配器按指令操作的数据填充到数据集中的某一工作表中（默认为“Table”工作表）
                Adapter.Fill(dtSet);


                //索引数据集中的第一个工作表对象
                System.Data.DataTable DataTable = dtSet.Tables[0]; // conn.GetSchema("Tables")

                //工作表中的数据有8列9行(它的范围与用Worksheet.UsedRange所得到的范围相同。
                //不一定是写有数据的单元格才算进行，对单元格的格式，如底纹，字号等进行修改的单元格也在其中。)
                int intRowsInTable = DataTable.Rows.Count;

                //提取每一行数据中的“成绩”数据
                string[] Data = new string[intRowsInTable - 1 + 1];
                for (int i = 0; i <= intRowsInTable - 1; i++)
                {
                    // 如果单元格中没有数据的话，则对应的数据的类型为System.DBNull
                    Data[i] = DataTable.Rows[i][0].ToString();
                }
                return Data;
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// 读取Excel工作表中的某一个字段数据
        /// </summary>
        /// <param name="conn">OleDB的数据连接</param>
        /// <param name="SheetName">要读取的数据所在的工作表，名称中请自行包括后缀$</param>
        /// <param name="FieldName">在读取的字段</param>
        /// <typeparam name="T">要提取的字段的数据类型，比如设置为 double? 等可空类型 </typeparam>
        /// <returns></returns>
        /// <remarks></remarks>
        public static T[] GetFieldDataFromExcel<T>(OleDbConnection conn, string SheetName, string FieldName)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                //创建向数据库发出的指令
                OleDbCommand olecmd = conn.CreateCommand();
                //类似SQL的查询语句这个[Sheet1$对应Excel文件中的一个工作表]
                //如果要提取Excel中的工作表中的某一个指定区域的数据，可以用："select * from [Sheet3$A1:C5]"
                olecmd.CommandText = "select * from [" + SheetName + "]";

                //创建数据适配器——根据指定的数据库指令
                OleDbDataAdapter Adapter = new OleDbDataAdapter(olecmd);

                //创建一个数据集以保存数据
                DataSet dtSet = new DataSet();

                //将数据适配器按指令操作的数据填充到数据集中的某一工作表中（默认为“Table”工作表）
                Adapter.Fill(dtSet);

                //其中的数据都是由 "select * from [" + SheetName + "$]"得到的Excel中工作表SheetName中的数据。
                int intTablesCount = dtSet.Tables.Count;

                //索引数据集中的第一个工作表对象
                System.Data.DataTable DataTable = dtSet.Tables[0]; // conn.GetSchema("Tables")

                //工作表中的数据有8列9行(它的范围与用Worksheet.UsedRange所得到的范围相同。
                //不一定是写有数据的单元格才算进行，对单元格的格式，如底纹，字号等进行修改的单元格也在其中。)
                int intRowsInTable = DataTable.Rows.Count;
                int intColsInTable = DataTable.Columns.Count;

                //提取每一行数据中的“成绩”数据
                T[] Data = new T[intRowsInTable - 1 + 1];
                for (int i = 0; i <= intRowsInTable - 1; i++)
                {
                    // 如果单元格中没有数据的话，则对应的数据的类型为System.DBNull
                    object v = DataTable.Rows[i][FieldName];

                    // 注意：Convert.IsDBNull(null)所返回的值为false
                    Data[i] = Convert.IsDBNull(v) ? default(T) : (T)v;
                }
                return Data;
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// 读取Excel工作簿中的多个字段的数据
        /// </summary>
        /// <param name="conn">OleDB的数据连接</param>
        /// <param name="SheetName">要读取的数据所在的工作表，工作表名请自行添加后缀“$”</param>
        /// <param name="FieldNames">在读取的每一个字段的名称</param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static DataTable GetFieldDataFromExcel(OleDbConnection conn, string SheetName, params string[] FieldNames)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                //创建向数据库发出的指令
                OleDbCommand olecmd = conn.CreateCommand();

                //类似SQL的查询语句这个[Sheet1$对应Excel文件中的一个工作表]
                //如果要提取Excel中的工作表中的某一个指定区域的数据，可以用："select * from [Sheet3$A1:C5]"
                olecmd.CommandText = "select " + ConstructFieldNames(FieldNames) + " from [" + SheetName + "]";

                //创建数据适配器——根据指定的数据库指令
                OleDbDataAdapter Adapter = new OleDbDataAdapter(olecmd);

                //创建一个数据集以保存数据
                DataSet dtSet = new DataSet();

                //将数据适配器按指令操作的数据填充到数据集中的某一工作表中（默认为“Table”工作表）
                Adapter.Fill(dtSet);

                //索引数据集中的第一个工作表对象
                DataTable DataTable = dtSet.Tables[0]; // conn.GetSchema("Tables")
                return DataTable;
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            return null;
        }

        /// <summary> 执行有参SQL语句，返回DataTable </summary>
        /// <param name="conn"></param>
        /// <param name="safeSql">数据查询语句，比如“ Selete * From [Sheet1$] ”</param>
        /// <returns> DataAdapter.Fill得到的DataSet中的第一个DataTable </returns>
        public static DataTable GetDataSet(OleDbConnection conn, string safeSql)
        {
            DataSet ds = new DataSet();
            OleDbCommand cmd = new OleDbCommand(safeSql, conn);
            OleDbDataAdapter da = new OleDbDataAdapter(cmd);
            da.Fill(ds);
            return ds.Tables[0];
        }

        #endregion

        #region 数据写入Excel

        /// <summary>
        /// 将一个全新的 DataTable 对象写入 Excel 数据库中
        /// </summary>
        /// <param name="conn"> </param>
        /// <param name="tableSource"> 数据源，此工作表中的每一个字段中的数据都会被插入到Excel的指定工作表中。
        /// 请手动确保工作表Sheet中有与DataTable中每一列同名的字段，而且其数据类型是兼容的。 </param>
        /// <param name="sheetName"> 要进行插入的Excel工作表的名称，其格式为“Sheet1$”。请确保此工作表已经存在，而且已经包含与 tableSource 的列名相对应的字段 </param>
        public static void InsertDataTable(OleDbConnection conn, DataTable tableSource, string sheetName)
        {

            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                string[] fields = new string[tableSource.Columns.Count];
                // 获取字段名
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i] = tableSource.Columns[i].ColumnName;
                }

                //创建向数据库发出的指令
                OleDbCommand olecmd = conn.CreateCommand();

                try
                {
                    string fieldNames = ConstructFieldNames(fields);
                    // 将DataTable中的每一行数据插入Excel工作表中的对应字段下。
                    foreach (DataRow row in tableSource.Rows)
                    {
                        StringBuilder sb = new StringBuilder();

                        sb.Append($"INSERT INTO [{sheetName}] ({fieldNames}) values ( ");
                        // 将要赋的值添加到sql语句中
                        ConstructDbValue(row.ItemArray, ref sb);
                        sb.Append(")");

                        // MessageBox.Show(sb.ToString(),"SQL 命令");

                        olecmd.CommandText = sb.ToString();
                        // 大致的效果是这样的。
                        // olecmd.CommandText = $"INSERT INTO [Sheet2$] (Field1, Field2, Field3) values (\'{row[0]}\',\'{row[1]}\',\'{row[1]}\')";

                        // 对于Excel，好像没有像SQL Server一样通过
                        // Insert Into[Sheet1$] ([Field3], [Field4]) VALUES(-1.475, -0.335), (1.2, 3.44)
                        // 来一次插入多条记录的方法，而且官方说明的案例中也是按上例中的为每一个Insert Into执行一次ExecuteNonQuery来实现多条记录的插入的。
                        olecmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("DataTable中的数据插入Excel工作表出错", ex);
                }
            }
        }

        /// <summary>
        /// 向Excel工作表中插入一条数据
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="sheetName">要插入数据的工作表名称，名称中请自带后缀$ </param>
        /// <param name="FieldName">要插入到的字段</param>
        /// <param name="Value">实际插入的数据</param>
        /// <remarks></remarks>
        public static void InsertToSheet(OleDbConnection conn, string sheetName, string FieldName, object Value)
        {
            string commandText = "insert into [" + sheetName + ("](" + FieldName) + ") values(\'" + Value + "\')";
            ExecuteNoneQuery(conn, commandText);
        }

        #endregion

        /// <summary>
        /// 根据指定的字段名创建一个全新的Excel工作表，但是不向其中添加任何数据。
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="sheetName"> 要创建的工作表的名称，不能带后缀$ </param>
        /// <param name="fields_valueTypes"> 工作表中的每一个字段，以及字段所对应的数据类型。如果不赋值，则只创建出一个工作表，而不创建任何字段。 </param>
        /// <remarks>在Excel中创建工作表的语句为： "CREATE TABLE Sheet1 ( [Field1] VarChar,[Field2] VarChar)" </remarks>
        public static void CreateNewSheet(OleDbConnection conn, string sheetName, params string[] fields_valueTypes)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (fields_valueTypes == null || fields_valueTypes.Length == 0)
            {
                // 向Excel中创建一个无字段的工作表时，虽然不会报错，但也不会生效。
                throw new InvalidCastException("请至少为工作表中添加一个字段");
            }

            if (fields_valueTypes.Length % 2 != 0)
            {
                throw new InvalidCastException("输入的字段与数据类型的数目不对应");
            }

            // 在Excel中创建工作表的语句为： "CREATE TABLE Sheet1 ( [Field1] VarChar,[Field2] VarChar)" 
            StringBuilder sb = new StringBuilder();
            sb.Append($"CREATE TABLE [{sheetName}] ([{fields_valueTypes[0]}] {fields_valueTypes[1]}");
            //
            for (int pair = 2; pair < fields_valueTypes.Length; pair += 2)
            {
                sb.Append($",[{fields_valueTypes[pair]}] {fields_valueTypes[pair + 1]}");
            }
            sb.Append(@")");

            ExecuteNoneQuery(conn, sb.ToString());

        }

        /// <summary>
        /// 根据指定的DataTable 创建一个全新的 Excel 工作表，而不添加任何数据。
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="tableSource"> 工作表名称即 tableSource.TableName；工作表中每一个字段的名称即tableSource中每一列的列名 </param>
        public static void CreateNewSheet(OleDbConnection conn, DataTable tableSource)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }

            string[] fields_valueTypes = new string[tableSource.Columns.Count * 2];

            for (int i = 0; i < tableSource.Columns.Count; i++)
            {
                fields_valueTypes[i * 2] = tableSource.Columns[i].ColumnName;
                fields_valueTypes[i * 2 + 1] = ConvertExcelDataType(tableSource.Columns[i].DataType);//"char(255)";//tableSource.Columns[i].DataType.ToString();
            }

            // 创建全新的Excel工作表
            CreateNewSheet(conn, tableSource.TableName, fields_valueTypes);
        }

        /// <summary> 对Excel数据库执行非查询SQL语句 </summary>
        /// <param name="conn"></param>
        /// <param name="sql"> 用来执行的非查询sql语句</param>
        /// <remarks></remarks>
        private static void ExecuteNoneQuery(OleDbConnection conn, string sql)
        {
            //如果连接已经关闭，则先打开连接
            if (conn.State == ConnectionState.Closed)
            {
                conn.Open();
            }
            if (conn.DataSource != null && IsExcelDataSource(conn.DataSource))
            {
                using (OleDbCommand ole_cmd = conn.CreateCommand())
                {
                    ole_cmd.CommandText = sql;
                    try
                    {
                        //在工作簿中创建新表格时，Excel工作簿不能处于打开状态
                        ole_cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("创建Excel文档失败，错误信息： " + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
            }
            else
            {
                MessageBox.Show("未正确连接到Excel数据库!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        #region ---   子函数

        /// <summary>
        /// 将要提取的字段名称转换为SQL语句中的字段名称字符
        /// </summary>
        /// <param name="FieldNames"></param>
        /// <returns></returns>
        private static string ConstructFieldNames(IList<string> FieldNames)
        {
            string names = "";
            if (FieldNames.Count >= 1)
            {
                names = @"[" + FieldNames[0] + @"]";
            }
            for (int i = 1; i < FieldNames.Count; i++)
            {
                names += @", [" + FieldNames[i] + @"]";
            }
            return names;
        }

        /// <summary>
        /// 将要提取的字段名称转换为SQL语句中的字段名称字符
        /// </summary>
        /// <param name="values"></param>
        /// <param name="sb"></param>
        private static void ConstructDbValue(IList<object> values, ref StringBuilder sb)
        {
            // 第一个值
            if (values.Count > 0)
            {
                // 不能将空字符串赋值给可为空的double类型字段，要先将空字符转换为null
                sb.Append(Convert.IsDBNull(values[0]) ? "null" : "\'" + values[0] + "\'");
            }

            // 后面的值
            for (int i = 1; i < values.Count; i++)
            {
                // 注意这里有一个“,”的区别
                sb.Append(Convert.IsDBNull(values[i]) ? ",null" : ",\'" + values[i] + "\'");
            }
        }

        /// <summary>
        /// Create Table时，为字段名匹配字段类型
        /// </summary>
        /// <param name="type"> .NET 中的数据类型 </param>
        /// <returns> Excel中的数据类型 </returns>
        private static string ConvertExcelDataType(Type type)
        {
            if (type == typeof(float) || type == typeof(int) || type == typeof(double))
            {
                return "double";
            }
            if (type == typeof(DateTime))
            {
                return "date";
            }
            return "char(255)";
        }

        #endregion
    }
}