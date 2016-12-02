using System;
using System.Linq.Expressions;
using System.Web.Http;
using HTM.Net.Research.Taurus.HtmEngine.Repository;

namespace HTM.Example.Taurus.Api.Controllers
{
    [Route("api/MetricData")]
    public class MetricDataController : ApiController
    {
        /// <summary>
        /// Get Model Data
        /// GET /_models/{model-id}/data?from={fromTimestamp}&to={toTimestamp}&anomaly={anomalyScore}&limit={numOfRows}
        /// </summary>
        /// <param name="id">model-id</param>
        /// <param name="from">timestamp (optional)</param>
        /// <param name="to">timestamp (optional)</param>
        /// <param name="anomaly">anomaly score to filter</param>
        /// <param name="limit">(optional) max number of records to return</param>
        /// <returns>
        /// {
        ///        "data": [
        ///            ["2013-08-15 21:34:00", 222, 0.025, 125],
        ///            ["2013-08-15 21:32:00", 202, 0, 124],
        ///            ["2013-08-15 21:30:00", 202, 0, 123],
        ///            ...
        ///        ],
        ///        "names": [
        ///            "timestamp",
        ///            "value",
        ///            "anomaly_score",
        ///            "rowid
        ///        ]
        ///}
        /// </returns>
        [HttpGet]
        public IHttpActionResult Get(string id, DateTime? from, DateTime? to, double anomaly = 0.0, int limit = 0)
        {
            Expression<Func<MetricData, object>> sort = metricData => metricData.Timestamp;
            var sortAsc = @from.HasValue;

            var result = RepositoryFactory.MetricData.GetMetricData(metricId: id, fromTimestamp: from, toTimestamp: to,
                score: anomaly, limit: limit, sort: sort, sortAsc: sortAsc);


            throw new NotImplementedException();
        }
    }
}