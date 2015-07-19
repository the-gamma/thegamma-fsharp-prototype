The world's biggest polluters
=============================

Which countries of the world are responsible for the global
warming? Looking at the data over the last 50 years tells us
quite a lot - not just about the biggest polluters, but also
about how the world has changed!

Carbon emissions today
----------------------

If we look at CO2 emissions for the whole world for recent years,
we can see the expected results. In general, the rich countries
in America and Europe together with China and India are producing
large percentage of the world's CO2 emissions. The map also shows
the next group: Iran, Australia, Canada, Brazil and Saudi Arabia:

    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    let co2 =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``

    chart.geo(co2)
      .colorAxis(colors=colorScale).show()

The largest producer of carbon dioxide (CO2) in the world is China.
In the year 2010, it produced over 8 millions of kilotons of CO2.
The list of the biggest polluters continues with the United States
(producing 5.4 millions), India (1.9 millions) and Russia. You can
see more in the table below.

A curious fact is that the smallest producer of CO2 emissions is
Liechtenstein with just 58 kilotons per year, followed by small
island countries of the Pacific and Africa.

    let countries =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``
        .sortValues(reverse=true)
        .take(10)

    table.create(countries).show()

If we look at the share of CO2 emissions for individual countries, we
can see that China produces over one quarter (26.4%) of all the CO2 of the
world. In fact, the first three countries (China, US and India) are
responsible for almost an exact half (49.9%) of the world's CO2:

    let countries =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``.sortValues(reverse=true)

    let sumRest =
      countries.skip(6).sum()
    let topAndRest =
      countries.take(6)
        .append("Other countries", sumRest)
    chart.pie(topAndRest).show()

Looking at the data for the year 2010 shows the expected results
with China, USA and India on the top of the list. But this is also
because these three are very large countries.

Emissions per capita
--------------------

To see a somewhat different picture, let's look at carbon emissions
per capita. That is, the CO2 emissions for a single person living
in each of the countries. As you can see in the following map, a
very different picture appears:

    let climate =
      world.byYear
        .``2010``.``Climate Change``
    let co2 =
      climate.``CO2 emissions (kt)``
    let population =
      climate.``Total Population (in number of people)``

    let pcp =
      co2.joinInner(population)
        .map(fun (co2, pop) -> co2 / pop)

    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    chart.geo(pcp)
      .colorAxis(colors=colorScale).show()

Quite different set of countries appear when compared to the
previous visualization. The biggest polluters per capita include
small Persian Gulf states including Quatar, Kuwait, UAR and Oman,
followed by large developed countries (USA, Canada and Australia).
We can still see China among the polluters too, but India almost
completely disappears.

Carbon emissions in the past
----------------------------

Although the biggest polluters of the modern world, and especially
China, are contributing a large part of the overall emissions
today, this only started happening in recent years. If we look at
the past, we see yet another picture. For example, in the year
1960, the biggest polluter by far was the United States.

The following map shows the data for the year 1960. The World Bank
does not have the data for the former Easter block countries,
and so the values there are missing.

    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    let co2 =
      world.byYear.``1960``.``Climate Change``
        .``CO2 emissions (kt)``

    chart.geo(co2)
      .colorAxis(colors=colorScale).show()

So, when did China become the biggest polluter in the world?
Quite surprisingly, this is only a recent development. As you
can see in our final visualization, the CO2 emissions of China
began growing rapidly around the year 2002, it overtook the
USA around 2005 and it continues growing.

    let topCountries =
      [ world.byCountry.China
        world.byCountry.India
        world.byCountry.Japan
        world.byCountry.``Russian Federation``
        world.byCountry.``United States`` ]

    let growths =
      topCountries.map(fun p ->
        p.``Climate Change``.``CO2 emissions (kt)``
          .set(seriesName=p.name) )

    chart.line(growths).show()
