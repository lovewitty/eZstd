﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SysMarshal = System.Runtime.InteropServices.Marshal;

namespace eZstd.MarshalReflection
{
    /// <summary>
    /// 在多种不同的进程或者 COM 对象之间进行交互
    /// </summary>
    public class Interaction
    {

        /// <summary> 根据程序名字符创建对应的COM对象 </summary>
        /// <param name="progId">比如 Excel.Application 或 Word.Application。</param>
        /// <returns></returns>
        public static object test(string progId)
        {
            return null;
        }

        /// <summary> 根据程序名字符创建对应的COM对象 </summary>
        /// <param name="progId">比如 Excel.Application 或 Word.Application。</param>
        /// <returns></returns>
        public static object GetObjectFromProgId(string progId)
        {
            object obj;
            if (!string.IsNullOrEmpty(progId))
            {
                // 方法一：
                // obj = Activator.CreateInstance(System.Type.GetTypeFromProgID(progId));

                // 方法二：
                obj = SysMarshal.GetActiveObject(progId);
            }
            else
            {
                obj = null;
            }
            return obj;
        }

  
        /// <summary>
        /// 打开某文件（不管是否已经打开）所对应的COM对象。
        /// </summary>
        /// <typeparam name="TObj">如果输入的是Excel的文件名，则对应的返回类型为 Workbook；</typeparam>
        /// <param name="monikerName">某个文件的名称，比如“C:\tempData.xlsx”。
        /// 注意，当此文件已经被打开时，则此方法会直接返回打开了的那个文件所在的COM对象；
        /// 而如果此文件还未打开，则此方法会将此文件打开，然后再返回对应的COM对象。</param>
        /// <returns>如果打开不成功，则返回 null</returns>
        public static TObj GetObjectFromFile<TObj>(string monikerName)
        {
            TObj resObj = default(TObj);
            if (File.Exists(monikerName))
            {
                try
                {
                    object obj = SysMarshal.BindToMoniker(monikerName);
                    if (obj != null)
                    {
                        resObj = (TObj)obj;
                    }
                }
                catch (Exception)
                {
                    resObj = default(TObj);
                }
            }
            return resObj;
        }
    }
}
