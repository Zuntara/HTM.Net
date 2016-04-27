using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Research.Taurus.HtmEngine.Adapters
{
    public static class DataAdapterFactory
    {
        private static List<IDataSourceAdapter> _adapterRegistry = new List<IDataSourceAdapter>();
        /// <summary>
        /// Factory for Datasource adapters
        /// </summary>
        /// <param name="dataSource">datasource (e.g., "cloudwatch", "custom", ...)</param>
        /// <returns></returns>
        public static IDataSourceAdapter CreateDatasourceAdapter(string dataSource)
        {
            var adapter = _adapterRegistry.SingleOrDefault(a => a.Datasource.Equals(dataSource, StringComparison.InvariantCultureIgnoreCase));
            if (adapter == null && dataSource == "custom")
            {
                return new CustomDatasourceAdapter();
            }
            if (adapter == null)
            {
                throw new InvalidOperationException("Adapter not found");
            }
            return adapter;
        }

        public static void RegisterDatasourceAdapter(IDataSourceAdapter clientCls)
        {
            if (!_adapterRegistry.Contains(clientCls))
            {
                _adapterRegistry.Add(clientCls);
            }
        }

        public static void ClearRegistrations()
        {
            _adapterRegistry.Clear();
        }
    }
}