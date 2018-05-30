using System.Text;

namespace Microsoft.DotNet.Darc
{
    public class QueryParameter
    {
        public StringBuilder whereConditions;

        public StringBuilder loggingConditions;

        public QueryParameter()
        {
            whereConditions = new StringBuilder();
            loggingConditions = new StringBuilder();
        }
    }
}
