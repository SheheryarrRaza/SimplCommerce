using System.Collections.Generic;

namespace SimplCommerce.Module.SampleData.Data
{
    public interface ISqlRepository
    {
        void RunCommand(string command);

        void RunCommands(IEnumerable<string> command);

        void ClearTable(string tableName);

        IEnumerable<string> ParseCommand(IEnumerable<string> lines);

        IEnumerable<string> PostgresCommands(IEnumerable<string> lines);

        string GetDbConnectionType();
    }
}
