using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtrapalhanciaDatabase.Tables
{
    public interface ISQLiteTable<T, J> where T : class
    {
        static abstract string GetCreateTableString();
        static abstract T? GetInstance(J id);
    }
}
