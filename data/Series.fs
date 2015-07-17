namespace TheGamma.Series

[<ReflectedDefinition>]
type series<'k, 'v> = 
  { data : Async<('k * 'v)[]> 
    keyName : string
    valueName : string
    seriesName : string }
  member x.set(data, ?keyName, ?valueName, ?seriesName) = 
    { data = data 
      keyName = defaultArg keyName x.keyName
      valueName = defaultArg valueName x.valueName
      seriesName = defaultArg seriesName x.seriesName }
  member x.set(?keyName, ?valueName, ?seriesName) = 
    { x with 
        keyName = defaultArg keyName x.keyName
        valueName = defaultArg valueName x.valueName
        seriesName = defaultArg seriesName x.seriesName }

[<ReflectedDefinition>]
type series =
  static member create(data, keyName, valueName, seriesName) = 
    { data = data; keyName = keyName; valueName = valueName; seriesName = seriesName }
