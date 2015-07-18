/**************************************** Top panel and Spinner stuff ****************************************/

$(window).scroll(function () {
  if ($(this).scrollTop() > 1) {
    $('header').addClass("scrolled");
    $('#main').addClass("scrolled");
  } else {
    $('header').removeClass("scrolled");
    $('#main').removeClass("scrolled");
  }
});

function startSpinning(el)
{
  var counter = 0;
  var finished = false;
  function spin() {
    if (finished) {
      el.style.display = "none";
      return;
    } else {
      el.style.display = "block";
      var offset = counter * -21;
      el.style.backgroundPosition = "0px " + offset + "px";
      counter++; if (counter >= 19) counter = 0;
      setTimeout(spin, 100);
    }
  };
  setTimeout(spin, 500);
  return function () { finished = true; };
}

/**************************************** Common for editors/visualizers ****************************************/

function setSource(id, source, byHand)
{
  window[id + "_source"] = source;
  window[id + "_change"].forEach(function (f) { f(byHand); });
}

/**************************************** Setting up the editor ****************************************/

function refreshOutput(id)
{
  var source = window[id + "_source"];
  $.ajax({
    url: "/run", data: source, contentType: "text/fsharp",
    type: "POST", dataType: "text"
  }).done(function (data) {
    //finished = true;
    eval("(function(){ \n\
      var outputElementID = \"" + id + "\"\n\
      var blockCallback = function () {}; \n" +
      data + "\n\
    })()");
  });
}

function setupEditor(id) {
  var source = window[id + "_source"];
  var element = document.getElementById(id + "_editor");

  // Setup the CodeMirror editor with fsharp mode
  var editor = CodeMirror(element, {
    value: source, mode: 'fsharp', lineNumbers: false
  });
  editor.focus();

  // Update text when it is changed by visualizers
  var updating = false;
  editor.on("change", function () {
    if (updating) return;
    updating = true;
    setSource(id, editor.getValue(),true);
    updating = false;
  });
  window[id + "_change"].push(function() {
    if (updating) return;
    updating = true;
    var source = window[id + "_source"];
    editor.getDoc().setValue(source);
    updating = false;
  });


  // Helper to send request to our server
  function request(operation, line, col) {
    var url = "/" + operation;
    if (line != null && col != null) url += "?line=" + (line + 1) + "&col=" + col;
    return $.ajax({
      url: url, data: editor.getValue(),
      contentType: "text/fsharp", type: "POST", dataType: "JSON"
    });
  }

  // Translate code to JS and then evaluate the script
  /*
  var evaluateScript = function () {
    var counter = 0;
    var finished = false;
    function spin() {
      if (finished) {
        document.getElementById("spinner").style.display = "none";
        return;
      } else {
        document.getElementById("spinner").style.display = "block";
        var offset = counter * -21;
        document.getElementById("spinner").style.backgroundPosition = "0px " + offset + "px";
        counter++; if (counter >= 19) counter = 0;
        setTimeout(spin, 100);
      }
    };
    setTimeout(spin,500);

    $.ajax({
      url: "/run", data: editor.getValue(), contentType: "text/fsharp",
      type: "POST", dataType: "text"
    }).done(function (data) {
      finished = true;
      eval("(function(){ " + data + "})()");
    });
  };*/

  // Request type-checking and parsing errors from the server
  editor.compiler = new Compiler(editor, function () {
    request("check").done(function (data) {
      editor.compiler.updateMarkers(data.errors);
    });
  });

  // Request declarations & method overloads from the server
  editor.intellisense = new Intellisense(editor,
    function (position) {
      request("declarations", position.lineIndex, position.columnIndex).done(function (data) {
        editor.intellisense.setDeclarations(data.declarations);
      });
    },
    function (position) {
      request("methods", position.lineIndex, position.columnIndex).done(function (data) {
        editor.intellisense.setMethods(data.methods);
      });
    }
  );
  $(element).addClass("editor-cm-visible");
  return editor;
}

function switchEditor(id)
{
  var edEl = $("#" + id + "_editor");
  if (edEl.is(":hidden")) {
    edEl.show();
    if (window[id + "_cm_editor"] == null)
      window[id + "_cm_editor"] = setupEditor(id);
  }
  else {
    edEl.hide();
  }
}

/**************************************** Setting up the editor ****************************************/

function setupDocEditor(id) {
  var source = window[id + "_source"];
  var element = document.getElementById(id + "_editor");
  var editor = CodeMirror(element, {
    value: source, mode: 'markdown', lineNumbers: false
  });
  editor.focus();
  editor.on("change", function () {
    window[id + "_source"] = editor.getValue();
  });
  $(element).addClass("editor-cm-visible");
  return editor;
}

function setupRefresh(id)
{
  var source1 = window[id + "_source"];
  var lastRendered = source1;
  var intervalId = setInterval(function () {
    var source2 = window[id + "_source"];
    if (source1 == source2) {
      if (lastRendered != source2) {
        lastRendered = source2;
        $.ajax({
          url: "/markdown", data: source2, contentType: "text/markdown",
          type: "POST", dataType: "text"
        }).done(function (data) {
          document.getElementById(id + "_output_html").innerHTML = data;
        });
      }
    }
    source1 = source2;
  }, 1000);
  return intervalId;
}

function switchDocEditor(id) {
  var wrEl = $("#" + id + "_doc_wrapper");
  var tlEl = $("#" + id + "_tools");
  if (wrEl.is(":hidden")) {
    wrEl.show();
    tlEl.hide();

    var intervalId = setupRefresh(id);
    window[id + "_cleanup"] = function () { clearInterval(intervalId); };

    if (window[id + "_cm_editor"] == null)
      window[id + "_cm_editor"] = setupDocEditor(id);
  }
  else {
    window[id + "_cleanup"]();
    tlEl.show();
    wrEl.hide();
  }
}

/**************************************** Setting up the visualizers ****************************************/

function createVisualizer(id, multiple, vis) {
  var source = window[id + "_source"];
  var utils = new Utils();
  var documentationSide = new DocumentationSide();

  // Create chosen slect element and add options from the visualizer
  var sel = $(multiple?'<select multiple />':'<select />');
  vis.options.forEach(function (v) {
    $('<option />').text(v.member).val(v.member).appendTo(sel);
  });
  sel.val(vis.initial);
  $("#" + id + "_visual").append(sel);
  sel.chosen();

  // Code to update the source code when the selection is changed
  if (multiple)
  {
    var range = { startl: vis.range[0], startc: vis.range[1], endl: vis.range[2], endc: vis.range[3] };
    var prefix = vis.prefix.join(".");
    var origLength = range.endl - range.startl + 1;

    function updateSource1() {
      var lines = window[id + "_source"].split('\n');
      var names = sel.val();
      var newLines = [];
      var newEndc, newEndl;

      window[id + "_offsetf"] = function(l) {
        if (l > range.startl) return l + names.length - origLength;
        else return l;
      };

      for(var i = 0; i < lines.length-(range.endl-range.startl+1)+names.length; i++)
      {
        var line;
        var ni = i-range.startl+1;
        if (i == range.startl-1)
          line = lines[i].slice(0, range.startc) + prefix + "." + utils.escapeIdent(names[ni]);
        else if (i > range.startl-1 && ni < names.length-1)
          line = utils.repeat(' ', range.startc) + prefix + "." + utils.escapeIdent(names[ni]);
        else if (ni == names.length-1)
        {
          var suffix = lines[range.endl-1].slice(range.endc+1);
          line = utils.repeat(' ', range.startc) + prefix + "." + utils.escapeIdent(names[ni]) + suffix;
          newEndl = i + 1;
          newEndc = line.length - suffix.length - 1;
        }
        else if (ni > names.length-1)
          line = lines[i-names.length+(range.endl-range.startl+1)];
        else
          line = lines[i];
        newLines.push(line);
      }
      range.endl = newEndl;
      range.endc = newEndc;
      setSource(id, newLines.join("\n"),false);
    };
    sel.on("change", updateSource1);
  }
  else
  {
    var range = { line: vis.range[0], start: vis.range[1], end: vis.range[3] };
    function updateSource2() {
      var f = window[id + "_offsetf"];
      if (!f) f = function(l) { return l; };

      var lines = window[id + "_source"].split('\n');
      var line = lines[f(range.line) - 1];
      var name = utils.escapeIdent(sel.val());
      lines[f(range.line) - 1] = line.slice(0, range.start) + name + line.slice(range.end + 1);
      range.end = range.start + name.length - 1;
      setSource(id, lines.join("\n"),false);
    };
    sel.on("change", updateSource2);
  }

  // Update the documentation side bar when something happens
  function updateDocumentation(member) {
    vis.options.forEach(function (v) {
      if (v.member == member) {
        documentationSide.showDocumentation(v.documentation);
        documentationSide.moveElement(sel.parent().offset().top);
      }
    });
  }
  sel.on("chosen:hiding_dropdown", function() {
    documentationSide.showElement(false);
  });
  sel.on("chosen:showing_dropdown", function() {
    function getCurrent() {
      updateDocumentation($(".chosen-results .highlighted").text());
    }
    $(".chosen-search").on("keyup", getCurrent); // for single-choice
    $(".chosen-choices").on("keyup", getCurrent); // for multi-choice
    $(".chosen-results").on("mouseover", getCurrent);
  });
}

function setupVisualizer(id) {
  var source = window[id + "_source"];
  $.ajax({
    url: "/visualizers", data: source,
    contentType: "text/fsharp", type: "POST", dataType: "JSON"
  }).done(function (data) {
    if (data.hash == window[id + "_vis_hash"]) return;

    window[id + "_vis_hash"] = data.hash;
    $("#" + id + "_visual").empty();
    data.singleLevel.forEach(function (vis) { createVisualizer(id, false, vis); });
    data.list.forEach(function (vis) { createVisualizer(id, true, vis); });
  });
}

function switchVisualizer(id) {
  var visEl = $("#" + id + "_visual");
  if (visEl.is(":hidden")) {
    visEl.show();
    if (window[id + "_vis_created"] != true)
    {
      setupVisualizer(id);
      window[id + "_vis_created"]=true;
      window[id + "_change"].push(function (byHand) {
        if (!byHand) return;
        var source1 = window[id + "_source"];
        setTimeout(function () {
          var source2 = window[id + "_source"];
          if (source1 == source2)
            setupVisualizer(id);
        }, 5000)
      });
    }
  }
  else {
    visEl.hide();
  }
}
/**************************************** TheGamma.Data ****************************************/

function showCountryDocumentation(el, info)
{
    while (el.hasChildNodes()) {
        el.removeChild(el.lastChild);
    }

    var list = "";
    function append(k, v) {
        if (v != null && v != "") {
            v = v.replace("(all income levels)", ""); // drop unnecessary noise
            list += "<dt>" + k + "</dt><dd>" + v + "</dd>";
        }
    };
    append("Capital city", info.capital);
    append("Populaion", info.population);
    append("Income level", info.income);
    append("Region", info.region);

    el.innerHTML = "<h2>" + info.name + "</h2><dl>" + list + "</dl>";

    var map = document.createElement("div");
    map.className = "map";
    el.appendChild(map);
    var data = google.visualization.arrayToDataTable([
        ['Country', 'Value'], [info.name, 100]
    ]);
    var options = {
        tooltip: { trigger: 'none' },
        region: info.regionCode,
        colorAxis: { colors: ['white', '#404040'] },
        backgroundColor: '#f8f8f8',
        datalessRegionColor: '#e0e0e0',
        defaultColor: '#e0e0e0',
        legend: 'none'
    };
    var chart = new google.visualization.GeoChart(map);
    chart.draw(data, options);
}

var chartsToDraw = [];
var googleLoaded = false;
function drawChartOnLoad(f) {
  if (googleLoaded) f();
  else chartsToDraw.push(f);
};
google.load('visualization', '1', { 'packages': ['corechart'] });
google.setOnLoadCallback(function () {
  googleLoaded = true;
  for (var i = 0; i < chartsToDraw.length; i++) chartsToDraw[i]();
  chartsToDraw = undefined;
});

function drawChart(chart, data, id, callback) {
  drawChartOnLoad(function() {
    var ctor = eval("(function(a) { return new google.visualization." + chart.typeName + " (a); })");
    var ch = ctor(document.getElementById(id));
    if (chart.options.height == undefined)
      chart.options.height = 400;
    ch.draw(data, chart.options);
    callback();
  });
}
