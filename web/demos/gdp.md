Does GDP matter?
================

GDP is perhaps the best known economic indicator. It measures
the total value of all goods and services produced in the country over the
time period measured - typically a year. But how does this matter to the
people living there? In this analysis, we look how the GDP correlate with
the life expectancy in the countries of the world.

    let life =
      world.byYear.``2010``.Health
        .``Life expectancy at birth, total (years)``

    let extremes =
      life.sortValues(reverse=true).take(5)
        .append("...", nan)
        .append(life.sortValues(reverse=false).take(5).reverse())

    table.create(extremes).show()

foo

    let life =
      world.byYear.``2010``.Health
        .``Life expectancy at birth, total (years)``
    chart.bar(life.sortValues(reverse=true).take(10)).show()

More

    let x = world.byYear.``2010``.``Economy & Growth``.``GDP per capita (current US$)``.map(log10)
    let y = world.byYear.``2010``.Health.``Life expectancy at birth, total (years)``
    chart.scatter(x, y)
      .set(pointSize=3.0, colors=["#3B8FCC"])
      .hAxis(title="Log of " + x.seriesName)
      .vAxis(title=y.seriesName)
      .trendlines([options.trendline(opacity=0.5, lineWidth=10.0, color="#C0D9EA")])
      .show()
