namespace TheGamma.GoogleCharts

open System
open TheGamma
open TheGamma.Series
open TheGamma.GoogleCharts

open FunScript
open FunScript.TypeScript    

[<ReflectedDefinition>]
type chart =
  static member scatter(xval:series<_, _>, yval:series<_, _>) = 
    { Scatter.data = ChartData.twoValues xval yval; 
      typeName = "ScatterChart"; options = ScatterChartOptions.empty }

  static member geo(data:series<string, float>) = 
    { Geo.data = ChartData.oneKeyValue "string" data; 
      typeName = "GeoChart"; options = GeoChartOptions.empty }
  static member geo(data:series<string, float * float>) = 
    { Geo.data = ChartData.oneKeyTwoValues "string" data; 
      typeName = "GeoChart"; options = GeoChartOptions.empty }

  static member pie(data:series<string, float>) = 
    { Pie.data = ChartData.oneKeyValue "string" data; 
      typeName = "PieChart"; options = PieChartOptions.empty }

  static member bar(data:series<string, float>) = 
    { Bar.data = ChartData.oneKeyValue "string" data; 
      typeName = "BarChart"; options = BarChartOptions.empty }
  static member bar(data:seq<series<string, float>>) = 
    { Bar.data = ChartData.oneKeyNValues "string" data; 
      typeName = "BarChart"; options = BarChartOptions.empty }

  static member column(data:series<string, float>) = 
    { Column.data = ChartData.oneKeyValue "string" data; 
      typeName = "ColumnChart"; options = ColumnChartOptions.empty }
  static member column(data:seq<series<string, float>>) = 
    { Column.data = ChartData.oneKeyNValues "string" data; 
      typeName = "ColumnChart"; options = ColumnChartOptions.empty }

  static member line(data:series<string, float>) = 
    { Line.data = ChartData.oneKeyValue "string" data; 
      typeName = "LineChart"; options = LineChartOptions.empty }
  static member line(data:series<int, float>) = 
    { Line.data = ChartData.oneKeyValue "number" data; 
      typeName = "LineChart"; options = LineChartOptions.empty }
  static member line(data:seq<series<string, float>>) = 
    { Line.data = ChartData.oneKeyNValues "string" data; 
      typeName = "LineChart"; options = LineChartOptions.empty }
  static member line(data:seq<series<int, float>>) = 
    { Line.data = ChartData.oneKeyNValues "number" data; 
      typeName = "LineChart"; options = LineChartOptions.empty }
(*
  static member histogram(data) = 
    { Histogram.data = data; options = HistogramOptions.empty }
  static member area(data) = 
    { Area.data = data; options = AreaChartOptions.empty }
  static member annotation(data) = 
    { Annotation.data = data; options = AnnotationChartOptions.empty }
  static member steppedArea(data) = 
    { SteppedArea.data = data; options = SteppedAreaChartOptions.empty }
  static member bubble(data) = 
    { Bubble.data = data; options = BubbleChartOptions.empty }
  static member treeMap(data) = 
    { TreeMap.data = data; options = TreeMapOptions.empty }
  static member table(data) = 
    { Table.data = data; options = TableOptions.empty }
  static member timeline(data) = 
    { Timeline.data = data; options = TimelineOptions.empty }
  static member candlestick(data) = 
    { Candlestick.data = data; options = CandlestickChartOptions.empty }
*)

  static member show(chart:#Chart) = 
    Helpers.showChart(chart)