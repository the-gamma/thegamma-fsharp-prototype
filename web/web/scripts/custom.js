/**************************************** Spinner ****************************************/

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

function createVisualizer(id, vis) {
  var source = window[id + "_source"];
  var utils = new Utils();
  var documentationSide = new DocumentationSide();

  var visCompletion = $("<div class='vis-completion' />");
  var inputGroup = $("<div class='input-group' />");
  var input = $("<input type='text' class='form-control' />");
  var err = $('<span class="glyphicon glyphicon-remove form-control-feedback" aria-hidden="true" />');
  var btn = $("<button type='button' class='btn btn-default'><span class='caret'></span></button>");
  var groupBtn = $("<div class='input-group-btn' />");
  btn.appendTo(groupBtn);

  visCompletion.appendTo(inputGroup);
  input.appendTo(inputGroup);
  err.appendTo(inputGroup);
  groupBtn.appendTo(inputGroup);

  input.val(vis.initial);
  $("#" + id + "_visual").append(inputGroup)

  var range = { line: vis.range[0], start: vis.range[1], end: vis.range[3] };
  function updateSource() {
    var lines = window[id + "_source"].split('\n');
    var line = lines[range.line - 1];

    var name = input.val();
    if (!isNaN(name[0]) || utils.lastIndexOfAny(name, [' ', '[', ']', '.']) != -1) {
      name = '``' + name + '``';
    }

    lines[range.line - 1] = line.slice(0, range.start) + name + line.slice(range.end + 1);
    range.end = range.start + name.length - 1;
    setSource(id, lines.join("\n"),false);

    var error = true;
    vis.options.forEach(function (v) { if (v.member == input.val()) { error = false; } });
    if (error) inputGroup.addClass("has-error"); else inputGroup.removeClass("has-error");
  };
  input.on("input", updateSource);

  var ignoringHide;
  function hideCompletion() {
    if (ignoringHide) return;
    visCompletion.hide();
    document.body.removeEventListener('click', hideCompletion, true);
    documentationSide.showElement(false);
  }
  function showCompletion(hideOnClick) {
    visCompletion.children().show();
    visCompletion.show();
    if (hideOnClick) document.body.addEventListener('click', hideCompletion, true);
  }

  btn.click(function () { input.focus(); showCompletion(true); });
  input.focus(function () { showCompletion(false); });
  input.blur(hideCompletion);

  var touchSelected = null;
  var touchCount = null;

  input.keydown(function (evt) {
    if (evt.keyCode == 13) {
      documentationSide.showElement(false);
      hideCompletion();
      return;
    }
    if (evt.keyCode != 40 && evt.keyCode != 38) return;

    var current = -1;
    var children = visCompletion.children().filter(":visible");
    children.each(function (i, a) {
      if ($(a).hasClass("current")) current = i;
    });

    // 38 up, 40 down
    var prev = current;
    if (evt.keyCode == 40) current++;
    if (evt.keyCode == 38) current--;
    if (current < 0) current = 0;
    if (current >= children.length) current = children.length - 1;
    children.slice(prev, prev + 1).removeClass("current");
    children.slice(current, current + 1).addClass("current");

    var memberEl = children.slice(current, current + 1);
    visCompletion.scrollTop(memberEl.height() * (current < 5 ? 0 : current - 4));

    var member = memberEl.text();
    input.val(member);
    updateSource();
    vis.options.forEach(function (v) {
      if (v.member == member)
        documentationSide.showDocumentation(v.documentation);
    });
  });

  input.on("input", function () {
    var search = input.val().toLowerCase();
    visCompletion.children().each(function (_, ch) {
      ch = $(ch);
      if (ch.text().toLowerCase().indexOf(search) >= 0)
        ch.show();
      else
        ch.hide();
    });
  });

  vis.options.forEach(function (v) {
    var a = $("<a />");
    a.mouseover(function () {
        documentationSide.showDocumentation(v.documentation);
        documentationSide.moveElement(input.offset().top);
        ignoringHide = true;
        a.addClass("current");
      })
      .mouseout(function () {
        ignoringHide = false;
        a.removeClass("current");
      })
      .bind("touchstart", function () {
        if (touchSelected == v.member)
          touchCount++;
        else {
          touchSelected = v.member;
          touchCount = 0;
        }
      })
      .click(function () {
        if (touchSelected == null || touchCount == 1) {
          ignoringHide = false;
          hideCompletion();
          input.val(v.member);
          updateSource();
        }
      })
      .text(v.member).appendTo(visCompletion);
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
    data.visualizers.forEach(function (vis) { createVisualizer(id, vis); });
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
