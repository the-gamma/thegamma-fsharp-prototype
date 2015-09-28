    let colorScale =
      [ "#6CC627";"#DB9B3B";"#DB7532";
        "#DD5321";"#DB321C";"#E00B00"]

    let co2 =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``

    chart.geo(co2)
      .colorAxis(colors=colorScale).show()