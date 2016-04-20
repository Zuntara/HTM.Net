==================
HTM Engine Runtime
==================
::
    metric_collector => anomaly_service => application subscribers
          /\  ||                ||
          ||  ||                ||
          ||  ||                \/
          ||   \\=======> `metric_data`
          ||
          \/
    metric_streamer <=> [Model Swapper]
metric_collector
----------------
The module `metric_collector` will collect metric data from all data sources
using the appropriate [data adapter](../adapters) at the metric scheduled
interval.  Once the model processes the newly streamed data the results are
pushed to the `htmengine.model.data` exchange and stored in the `metric_data`
database table.
metric_streamer
---------------
The module `metric_streamer` will stream to the model associated with the
source metric.
anomaly_service
---------------
The module `anomaly_service` runs anomaly likelihood calculations and
broadcasts final model results to downstream services, such as
`notification_service`