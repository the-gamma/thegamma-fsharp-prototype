What countries do US states look like?
======================================

Select a US state:
<select id="usstates">
  <option></option>
  <option value='37253956.000000,423967.000000'>California</option>
  <option value='25145561.000000,695662.000000'>Texas</option>
  <option value='18801310.000000,170312.000000'>Florida</option>
  <option value='19378102.000000,141297.000000'>New York</option>
  <option value='12830632.000000,149995.000000'>Illinois</option>
  <option value='12702379.000000,119280.000000'>Pennsylvania</option>
  <option value='11536504.000000,116098.000000'>Ohio</option>
  <option value='9687653.000000,153910.000000'>Georgia</option>
  <option value='9535483.000000,139391.000000'>North Carolina</option>
  <option value='9883640.000000,250487.000000'>Michigan</option>
  <option value='8791894.000000,22591.000000'>New Jersey</option>
  <option value='8001024.000000,110787.000000'>Virginia</option>
  <option value='6724540.000000,184661.000000'>Washington</option>
  <option value='6547629.000000,27336.000000'>Massachusetts</option>
  <option value='6392017.000000,295234.000000'>Arizona</option>
  <option value='6483802.000000,94326.000000'>Indiana</option>
  <option value='6346105.000000,109153.000000'>Tennessee</option>
  <option value='5988927.000000,180540.000000'>Missouri</option>
  <option value='5773552.000000,32131.000000'>Maryland</option>
  <option value='5686986.000000,169635.000000'>Wisconsin</option>
  <option value='5303925.000000,225163.000000'>Minnesota</option>
  <option value='5029196.000000,269601.000000'>Colorado</option>
  <option value='4779736.000000,135767.000000'>Alabama</option>
  <option value='4625364.000000,82933.000000'>South Carolina</option>
  <option value='4533372.000000,135659.000000'>Louisiana</option>
  <option value='4339367.000000,104656.000000'>Kentucky</option>
  <option value='3831074.000000,254799.000000'>Oregon</option>
  <option value='3751351.000000,181037.000000'>Oklahoma</option>
  <option value='3725789.000000,13791.000000'>Puerto Rico</option>
  <option value='3574097.000000,14357.000000'>Connecticut</option>
  <option value='3046355.000000,145746.000000'>Iowa</option>
  <option value='2967297.000000,125438.000000'>Mississippi</option>
  <option value='2915918.000000,137732.000000'>Arkansas</option>
  <option value='2763885.000000,219882.000000'>Utah</option>
  <option value='2853118.000000,213100.000000'>Kansas</option>
  <option value='2700551.000000,286380.000000'>Nevada</option>
  <option value='2059179.000000,314917.000000'>New Mexico</option>
  <option value='1826341.000000,200330.000000'>Nebraska</option>
  <option value='1852994.000000,62756.000000'>West Virginia</option>
  <option value='1567582.000000,216443.000000'>Idaho</option>
  <option value='1360301.000000,28313.000000'>Hawaii</option>
  <option value='1328361.000000,91633.000000'>Maine</option>
  <option value='1316470.000000,24214.000000'>New Hampshire</option>
  <option value='1052567.000000,4001.000000'>Rhode Island</option>
  <option value='989415.000000,380831.000000'>Montana</option>
  <option value='897934.000000,6446.000000'>Delaware</option>
  <option value='814180.000000,199729.000000'>South Dakota</option>
  <option value='672591.000000,183108.000000'>North Dakota</option>
  <option value='710231.000000,1723337.000000'>Alaska</option>
  <option value='601723.000000,177.000000'>District of Columbia</option>
  <option value='625741.000000,24906.000000'>Vermont</option>
  <option value='563626.000000,253335.000000'>Wyoming</option>
  <option value='159358.000000,1478.000000'>Guam</option>
  <option value='55519.000000,1505.000000'>American Samoa</option>
  <option value='53883.000000,5117.000000'>Northern Mariana Islands</option>
</select>

<script type="text/javascript">
var outputElementID = "nada";
var blockCallback = function() {};
</script>
<script type="text/javascript" src="/us-states-source?export=summary"></script>


<script type="text/javascript">
  google.setOnLoadCallback(function() {
    var data = google.visualization.arrayToDataTable([ ['State', 'State'] ]);
    var options = {width: 556, height: 347, region: "US", resolution: "provinces"};
    var chart = new google.visualization.GeoChart(document.getElementById('states_div'));
    chart.draw(data, options);
  });
</script>


<script type="text/javascript">
  $(function() {
    var uss = $("#usstates");
    uss.chosen();
    uss.on("change", function() {
      //
      //  Draw the US map and highlight state
      //
      var state = $("#usstates option:selected").text();
      var data = google.visualization.arrayToDataTable([ ['State', 'State'], [state, 1] ]);
      var options = {width: 556, height: 347, region: "US", resolution: "provinces"};
      var chart = new google.visualization.GeoChart(document.getElementById('states_div'));
      chart.draw(data, options);
      //
      //  Fill the table with the similar countries
      //
      var attrs = uss.val().split(',');
      summary(attrs[0])(attrs[1])(function(res) {
        var out = $("#output");
        out.empty();
        var html = "<tr><th>" + state + "</th><td>" + numeral(attrs[0]).format("0,0.00") + "</td><td>" + numeral(attrs[1]).format("0,0.00") + "</td></tr>";
        $(html).appendTo(out);
        for(var i = 0; i < res.length; i++)
        {
          var country = res[i].Items[0];
          var pop = res[i].Items[1][0];
          var area = res[i].Items[1][1];
          var html = "<tr><th>" + country + "</th><td>" + numeral(pop).format("0,0.00") + "</td><td>" + numeral(area).format("0,0.00") + "</td></tr>";
          $(html).appendTo(out);
        }
        //
        //  Generate map with highlighted countries
        //
        var dataArr = [ ['Country', 'Country'] ];
        for(var i = 0; i < res.length; i++) dataArr.push([res[i].Items[0], 1]); 
        var data = google.visualization.arrayToDataTable(dataArr);
        var options = {width: 556, height: 347 };
        var chart = new google.visualization.GeoChart(document.getElementById('world_div'));
        chart.draw(data, options);
      });
    })
  });
</script>
<style type="text/css">
  table th { font-weight:bold; }
</style>
<div id="states_div"></div>
<br />

## Most similar countries in the world

Given a US state, what are the most similar countries in the world based on the population and area of the state?

<br />
<table class="table table-striped">
  <thead>
    <tr><th>Country</th><th>Population</th><th>Area</th></tr>
  </thead>
  <tbody id="output"></tbody>
</table>

<div id="world_div"></div>

<div id="nada" style="display:none"></div>
<br /><br /><br /><br /><br /><br /><br /><br />