using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Web.Http;
using HTM.Net.Research.Taurus.HtmEngine.Adapters;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Research.Taurus.MetricCollectors;

namespace HTM.Net.Taurus.Api.Controllers
{
    public class ModelsController : ApiController
    {
        // GET: api/Model/modelId
        /// <summary>
        /// List all models or a specific model if one is given
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        /// [{
        ///     "description": "DiskWriteBytes on EC2 instance i-12345678 in us-west-2 region",
        ///     "display_name": "htm-it-docs (i-12345678)",
        ///     "last_rowid": 4053,
        ///     "last_timestamp": "2013-12-12 00:00:00",
        ///     "location": "us-west-2",
        ///     "message": null,
        ///     "name": "AWS/EC2/DiskWriteBytes",
        ///     "parameters": "{"InstanceId": "i-12345678", "region": "us-west-2"}",
        ///     "poll_interval": 300,
        ///     "server": "i-12345678",
        ///     "status": 1,
        ///     "tag_name": "htm-it-docs",
        ///     "uid": "2a123bb1dd4d46e7a806d62efc29cbb9"
        ///   }, ...
        /// ]
        /// </returns>
        [HttpGet]
        public IHttpActionResult Get(string id = null)
        {
            string modelId = id;

            try
            {
                List<Metric> modelRows;
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    modelRows = GetAllModels();
                }
                else
                {
                    modelRows = new List<Metric>
                    {
                        GetModel(modelId)
                    };
                }
                return Ok(modelRows.Select(FormatMetricRowProxy));
            }
            catch (Exception e)
            {
                return InternalServerError(e);
            }
        }

        // POST: api/Model
        [HttpPost]
        public IHttpActionResult Post([FromBody]CreateModelRequest[] model)
        {
            return Put(null, model);
        }

        // PUT: api/Model/modelId
        [HttpPut]
        public IHttpActionResult Put(string id, [FromBody]CreateModelRequest[] model)
        {
            string modelId = id;
            if (modelId != null)
            {
                // ModelHandler is overloaded to handle both single-model requests, and
                // multiple-model requests.  As a result, if a user makes a POST, or PUT
                // request, it's possible that the request can be routed to this handler
                // if the url pattern matches.  This specific POST handler is not meant
                // to operate on a known model, therefore, raise an exception, and return
                // a `405 Method Not Allowed` response.
                //throw new NotAllowedResponse(new { result = "Not supported" });
                return StatusCode(HttpStatusCode.MethodNotAllowed);
            }
            List<Metric> response = new List<Metric>();
            if (model != null)
            {
                var request = model;

                foreach (CreateModelRequest nativeMetric in request)
                {
                    try
                    {
                        //Validate(nativeMetric);
                    }
                    catch (Exception)
                    {

                        throw;
                    }


                }
            }
            else
            {
                // Metric data is missing
                return BadRequest("Metric data missing");
            }

            try
            {
                // AddStandardHeaders() // TODO: check what this does.
                var metricRowList = CreateModels(model);
                List<Metric> metricDictList = metricRowList.Select(FormatMetricRowProxy).ToList();
                response = metricDictList;

                return Created("", response);
            }
            catch (Exception)
            {
                // TODO: log as error
                throw;
            }
        }

        // DELETE: api/Model/modelId
        [HttpDelete]
        public IHttpActionResult Delete(string id)
        {
            string modelId = id;
            try
            {
                if (DeleteModel(modelId))
                    return Ok();
                return BadRequest("Something went wrong");
            }
            catch (Exception e)
            {
                return InternalServerError(e);
            }
        }

        #region Helper Methods

        private Metric FormatMetricRowProxy(Metric metricObj)
        {
            string displayName;
            if (!string.IsNullOrEmpty(metricObj.TagName))
            {
                displayName = string.Format("{0} ({1})", metricObj.TagName, metricObj.Server);
            }
            else
            {
                displayName = metricObj.Server;
            }
            var parameters = metricObj.Parameters;

            string[] allowedKeys = GetMetricDisplayFields();

            Metric metricDict = metricObj.Clone(allowedKeys);
            metricDict.Parameters = parameters;
            metricDict.DisplayName = displayName;

            return metricDict;
        }

        public string[] GetMetricDisplayFields()
        {
            return new[]
            {
                "uid", "datasource", "name",
                "description",
                "server",
                "location",
                "parameters",
                "status",
                "message",
                "lastTimestamp",
                "pollInterval",
                "tagName",
                "lastRowid"
            };
        }

        private static List<Metric> CreateModels(CreateModelRequest[] request = null)
        {
            if (request != null)
            {
                List<Metric> response = new List<Metric>();
                foreach (var nativeMetric in request)
                {
                    try
                    {
                        response.Add(ModelHandler.CreateModel(nativeMetric));
                    }
                    catch (Exception)
                    {
                        // response.append("Model failed during creation. Please try again.")
                        throw;
                    }
                }
                return response;
            }
            throw new NotImplementedException("bad request");
        }

        private List<Metric> GetAllModels()
        {
            return RepositoryFactory.Metric.GetAllModels();
        }

        private Metric GetModel(string metricId)
        {
            return RepositoryFactory.Metric.GetMetric(metricId);
        }

        private bool DeleteModel(string metricId)
        {
            var metricRow = RepositoryFactory.Metric.GetMetric(metricId);

            DataAdapterFactory.CreateDatasourceAdapter(metricRow.DataSource).UnmonitorMetric(metricId);

            return true;
        }

        #endregion
    }

    public class MetricDataController
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
        public IHttpActionResult Get(string id, DateTime? from, DateTime? to, double anomaly = 0.0, int limit = 0)
        {
            Expression<Func<MetricData, object>> sort = metricData => metricData.Timestamp;
            var sortAsc = @from.HasValue;

            var result = RepositoryFactory.MetricData.GetMetricData(metricId: id, fromTimestamp:from, toTimestamp: to, 
                score: anomaly, limit: limit, sort: sort, sortAsc: sortAsc);


            throw new NotImplementedException();
        }
    }
}