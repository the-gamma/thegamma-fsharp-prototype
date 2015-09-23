- title : The Gamma
- description : Data journalism
- author : Tomas Petricek
- theme : night
- transition : none

***************************************************************************************************
 - class: intro

<style type="text/css">
.navigate-left, .navigate-up, .navigate-right, .navigate-down {
  display:none;
}
.reveal table.pre {
  margin-left:20px;
}
.reveal .intro h1, .reveal h2, .reveal p {
  text-align:left;
}
.reveal h2 {
  font:bold 40pt Rufina,'Times New Roman', Times, serif;
  margin-bottom:60px;
}
.reveal ul {
  width:100%;
  padding-left:20px;
}
.reveal p {
  font: 30pt 'Pontano Sans','Trebuchet MS', sans-serif;
}
.reveal a {
  color:#ddc060;
  text-decoration:none;
}
.reveal a:hover {
  color:#f0c040;
}
.reveal .sep {
  padding:0px 6px 0px 6px;
}
.reveal p strong, .reveal li strong {
  color:#ddc060;
  font-weight:normal;
  text-style:normal;
}
.reveal p em, .reveal li em {
  color:#78AEDB;
  font-weight:normal;
  text-style:normal;
}
.reveal .intro p strong {
  color:white;
  font-weight:bold;
}
.reveal .slides > section {
  width:800px;
  margin-left:120px;
}
.reveal section img { 
  border-style:none;
  background:transparent;
}
.summary p {
  font-size:24pt;
}
.reference p { 
  font-size:14pt;
  text-align:center;
}
.reveal blockquote { 
  background:transparent;
  margin:0px;
  padding:0px;
  width:100%;
  text-align:left;
}
.reveal blockquote em { 
  font-style:italic; 
}
.reveal blockquote strong {
  font-weight:bold;
}
</style>

# The Gamma

[www.thegamma.net](http://thegamma.net)

<br /><br /><br /><br />

**Tomas Petricek** <br />
[_@tomaspetricek_](http://twitter.com/tomaspetricek) <span class="sep">|</span>
[_tomas@tomasp.net_](mailto:tomas@tomasp.net)

***************************************************************************************************

# Data journalism

---------------------------------------------------------------------------------------------------

<img src="images/dj.png" />

<div class="reference">

Illustration from: [Data journalism handbook](http://datajournalismhandbook.org/)

</div>

---------------------------------------------------------------------------------------------------

<a href="http://www.theguardian.com/news/datablog/2015/jun/24/air-pollution-delhi-is-dirty-but-how-do-other-cities-fare">
<img src="images/delhi.png" style="height:550px"/>
</a>

<div class="reference">

The Guardian: [Air pollution: Delhi is dirty, but how do other cities fare?](http://www.theguardian.com/news/datablog/2015/jun/24/air-pollution-delhi-is-dirty-but-how-do-other-cities-fare)

</div>

---------------------------------------------------------------------------------------------------

## Data driven articles

<div class="fragment">

Not just text with visualizations, but...

</div><div class="fragment">

 - Can it be _modified and reproduced_?
 - Is the _source code_ available?
 - Are the _data sources_ referenced?

</div><div class="fragment">
<br />

> **Data driven article is really a program!**

<br /><br /></div>

***************************************************************************************************

# The Gamma

---------------------------------------------------------------------------------------------------

### Data driven article in The Gamma

<a href="http://thegamma.net/carbon">
<img src="images/gamma.png" style="height:400px"/>
</a>

<div class="reference">

The Gamma: [The world's biggest polluters](http://thegamma.net/carbon)  

</div>

---------------------------------------------------------------------------------------------------

## Article is a program view

<div class="fragment">

There is no magic. It is just [code and text](https://github.com/tpetricek/TheGamma/blob/master/web/demos/carbon.md)!

</div><div class="fragment">

 - We see the _journalist's story_ first
 - Readers can _modify the parameters_
 - Power users can _see and modify the code_

<br /><br /></div>

---------------------------------------------------------------------------------------------------

## Programming language research

<div class="fragment">

Typed functional language with type providers

</div><div class="fragment">

 - Simple code with _functional programming_
 - Editor support via **static types** 
 - Data access with _F# type providers_

<br /><br /></div>

***************************************************************************************************

# Technology behind

---------------------------------------------------------------------------------------------------

## World bank data source

<img src="images/wb1.png" style="height:450px;position:relative;top:-20px;left:-50px"/>

---------------------------------------------------------------------------------------------------

## World bank data source

<img src="images/wb2.png" style="height:450px;position:relative;top:-20px;left:-50px"/>

---------------------------------------------------------------------------------------------------

## World bank data source

<img src="images/wb3.png" style="height:450px;position:relative;top:-20px;left:-50px"/>

---------------------------------------------------------------------------------------------------

## World bank data source

<img src="images/wb4.png" style="height:450px;position:relative;top:-20px;left:-50px"/>

---------------------------------------------------------------------------------------------------

## World bank type provider

<div class="fragment">
<img src="images/diagram.png" style="height:450px;position:relative;top:-20px;left:-50px"/>
</div>

---------------------------------------------------------------------------------------------------

## Auto-generated options

<div class="fragment">

One of members of the same type

    let co2 =
      world.byYear.``2010``.``Climate Change``
        .``CO2 emissions (kt)``
        
</div><div class="fragment">

List with sub-set of properties

    let topCountries =
      [ world.byCountry.China
        world.byCountry.India
        world.byCountry.Japan ]    

</div>

***************************************************************************************************

# Future directions

---------------------------------------------------------------------------------------------------

## Future directions

<div class="fragment">

Programming experiences research

<div class="reference" style="position:absolute;top:450px;left:20px">

Programming experiences: [Sean McDirmid on Lambda the Ultimate](http://lambda-the-ultimate.org/node/5247)

</div>
</div><div class="fragment">

 - Usable _literate programming_
 - Use PL for **provenance, context** and more!
 - Mapping for _large-scale open government data_
 - Grammar of **interactive visualizations**
 
<br /><br /></div>



***************************************************************************************************

## Summary

<div class="fragment">

Is programming the new literacy?

</div><div class="fragment">

 - _Understanding_ information is
 - **Programming** needs to evolve
 - Check out the _prototype_!

<br /><br /><br /></div>

<div class="summary">

[thegamma.net](http://thegamma.net) <span class="sep">|</span>
[_@tomaspetricek_](http://twitter.com/tomaspetricek) <span class="sep">|</span>
[_tomas@tomasp.net_](mailto:tomas@tomasp.net)

</div>
