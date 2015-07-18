World's biggest polluters
=========================

In this demonstration, we look at the countries that are producing the largest
amounts of CO2 emissions. Which of the countries in the world are the largest
contributors to the global pollution? And what is the difference between
developed and developing countries? First, let's look at the absolute carbon
emissions per country:

    let co2 =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``

    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    chart.geo(co2)
      .colorAxis(colors=colorScale).show()

Table

    let countries =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``
        .sortValues(reverse=true)
        .take(10)

    table.create(countries).show()

If we look at the totals...

    let countries =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``.sortValues(reverse=true)

    let sumRest =
      countries.skip(6).sum()
    let topAndRest =
      countries.take(6)
        .append("Other countries", sumRest)
    chart.pie(topAndRest).show()


This shows the expected results. The world's biggest polluters are China,
followed by the USA, India and Russia. The next polluters are smaller and
include Germany, UK, Canada and Brazil. This shows the total emissions and so
larger countries appear as bigger polluters. A different picture appears when
we look at emissions per capita:

    let co2 =
      world.byYear
        .``2010``.``Climate Change``
        .``CO2 emissions (kt)``
    let population =
      world.byYear
        .``2010``.``Climate Change``
        .``Total Population (in number of people)``

    let pcp =
      co2.joinInner(population)
        .map(fun (co2, pop) -> co2 / pop)

    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    chart.geo(pcp)
      .colorAxis(colors=colorScale).show()

Here, we can see quite different picture from the previous visualization. The
biggest polluters per capita include small Persian Gulf states including Quatar,
Kuwait, UAR and Oman, followed by large developed countries (USA, Canada and
Australia). We can still see China among the polluters too, but India almost
completely disappears from this picture.

Who is growing?

    let topCountries =
      [ world.byCountry.China
        world.byCountry.India
        world.byCountry.``United States``
        world.byCountry.``Russian Federation``
        world.byCountry.Germany
        world.byCountry.``United Kingdom``
        world.byCountry.Canada
        world.byCountry.Brazil ].series()

    let growths =
      topCountries.mapTask(fun p ->
          p.``Climate Change``.``Population growth (annual %)``.first())
        .sortValues(reverse=true)
        .set(seriesName="Population growth (annual %)")

    chart.column(growths)
      .set(colors=["#DB9B3B"]).show()

Yo

TODO: Demo with map on tuples!