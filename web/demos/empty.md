New article
===========

Click on the `edit` button above to change the heading and the text of the
article. Click on the `source` button below to add a visualization.

    // Use 'world' to get World Bank data
    // Use 'chart' or 'table' for visualizations
//    empty.show()
    let cz = world.byCountry.``Czech Republic``
    let uni = cz.Education.``School enrollment, tertiary (% gross)``
    chart.line(uni).show()
