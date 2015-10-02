What countries do US states look like?
======================================

This is the source code for [an interactive visualization](/us-states) that finds the most
similar coutnries for a given US state based on its population and area. The table below shows
the results for California.

    let pop = world.byYear.``2010``.``All indicators``.``Population, total``
    let area = world.byYear.``2010``.``All indicators``.``Land area (sq. km)``
    let countries = pop.joinInner(area)

    let mostSimilar population area = 
      countries
        .sortBy(fun (p, a) ->
          abs(1.0-p/population) + abs(1.0-a/area))
        .map(fun (p, a) -> [| p; a |])
        .take(5)

    let summary population area callback = 
      let top5 = mostSimilar population area 
      async { 
        let! r = top5.data    
        callback r } |> Async.StartImmediate

    let calpop, calarea = 38800000.0, 423970.0
    table.create(mostSimilar calpop calarea).show() 

<br /><br /><br /><br /><br /><br /><br />
<br /><br /><br /><br /><br /><br /><br />
<br /><br /><br /><br /><br /><br /><br />
<br /><br /><br /><br /><br /><br /><br />
