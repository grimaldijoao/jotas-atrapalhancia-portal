namespace AtrapalhanciaDatabase.Tables
{
    public interface ISQLMigratable
    {
        static abstract string GetCreateTableString();
    }

    public interface ISQLiteTable<T, J> : ISQLMigratable where T : class
    {
        static abstract T? GetInstance(J id);
    }
}
