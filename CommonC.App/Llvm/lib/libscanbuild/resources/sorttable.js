/*
  SortTable
  version 2
  7th April 2007
  Stuart Langridge, http://www.kryogenix.org/code/browser/sorttable/

  Instructions:
  Download this file
  Add <script src="sorttable.js"></script> to your HTML
  Add class="sortable" to any table you'd like to make sortable
  Click on the headers to sort

  Thanks to many, many people for contributions and suggestions.
  Licenced as X11: http://www.kryogenix.org/code/browser/licence.html
  This basically means: do what you want with it.
*/

var stIsIE = /*@cc_on!@*/ false;

sorttable = {
  init : function() {
    // quit if this function has already been called
    if (arguments.callee.done)
      return;
    // flag this function so we don't do the same thing twice
    arguments.callee.done = true;
    // kill the timer
    if (_timer)
      clearInterval(_timer);

    if (!document.createElement || !document.getElementsByTagName)
      return;

    sorttable.DATE_RE = /^(\d\d?)[\/\.-](\d\d?)[\/\.-]((\d\d)?\d\d)$/;

    forEach(document.getElementsByTagName('table'), function(table) {
      if (table.className.search(/\bsortable\b/) != -1) {
        sorttable.makeSortable(table);
      }
    });
  },

  makeSortable : function(table) {
    if (table.getElementsByTagName('thead').length == 0) {
      // table doesn't have a tHead. Since it should have, create one and
      // put the first table row in it.
      the = document.createElement('thead');
      the.appendChild(table.rows[0]);
      table.insertBefore(the, table.firstChild);
    }
    // Safari doesn't support table.tHead, sigh
    if (table.tHead == null)
      table.tHead = table.getElementsByTagName('thead')[0];

    if (table.tHead.rows.length != 1)
      return; // can't cope with two header rows

    // Sorttable v1 put rows with a class of "sortbottom" at the bottom (as
    // "total" rows, for example). This is B&R, since what you're supposed
    // to do is put them in a tfoot. So, if there are sortbottom rows,
    // for backward compatibility, move them to tfoot (creating it if needed).
    sortbottomrows = [];
    for (var i = 0; i < table.rows.length; i++) {
      if (table.rows[i].className.search(/\bsortbottom\b/) != -1) {
        sortbottomrows[sortbottomrows.length] = table.rows[i];
      }
    }
    if (sortbottomrows) {
      if (table.tFoot == null) {
        // table doesn't have a tfoot. Create one.
        tfo = document.createElement('tfoot');
        table.appendChild(tfo);
      }
      for (var i = 0; i < sortbottomrows.length; i++) {
        tfo.appendChild(sortbottomrows[i]);
      }
      delete sortbottomrows;
    }

    // work through each column and calculate its type
    headrow = table.tHead.rows[0].cells;
    for (var i = 0; i < headrow.length; i++) {
      // manually override the type with a sorttable_type attribute
      if (!headrow[i].className.match(
              /\bsorttable_nosort\b/)) { // skip this col
        mtch = headrow[i].className.match(/\bsorttable_([a-z0-9]+)\b/);
        if (mtch) {
          override = mtch[1];
        }
        if (mtch && typeof sorttable["sort_" + override] == 'function') {
          headrow[i].sorttable_sortfunction = sorttable["sort_" + override];
        } else {
          headrow[i].sorttable_sortfunction = sorttable.guessType(table, i);
        }
        // make it clickable to sort
        headrow[i].sorttable_columnindex = i;
        headrow[i].sorttable_tbody = table.tBodies[0];
        dean_addEvent(headrow[i], "click", function(e) {
          if (this.className.search(/\bsorttable_sorted\b/) != -1) {
            // if we're already sorted by this column, just
            // reverse the table, which is quicker
            sorttable.reverse(this.sorttable_tbody);
            this.className = this.className.replace('sorttable_sorted',
                                                    'sorttable_sorted_reverse');
            this.removeChild(document.getElementById('sorttable_sortfwdind'));
            sortrevind = document.createElement('span');
            sortrevind.id = "sorttable_sortrevind";
            sortrevind.innerHTML = stIsIE
                                       ? '&nbsp<font face="webdings">5</font>'
                                       : '&nbsp;&#x25B4;';
            this.appendChild(sortrevind);
            return;
          }
          if (this.className.search(/\bsorttable_sorted_reverse\b/) != -1) {
            // if we're already sorted by this column in reverse, just
            // re-reverse the table, which is quicker
            sorttable.reverse(this.sorttable_tbody);
            this.className = this.className.replace('sorttable_sorted_reverse',
                                                    'sorttable_sorted');
            this.removeChild(document.getElementById('sorttable_sortrevind'));
            sortfwdind = document.createElement('span');
            sortfwdind.id = "sorttable_sortfwdind";
            sortfwdind.innerHTML = stIsIE
                                       ? '&nbsp<font face="webdings">6</font>'
                                       : '&nbsp;&#x25BE;';
            this.appendChild(sortfwdind);
            return;
          }

          // remove sorttable_sorted classes
          theadrow = this.parentNode;
          forEach(theadrow.childNodes, function(cell) {
            if (cell.nodeType == 1) { // an element
              cell.className =
                  cell.className.replace('sorttable_sorted_reverse', '');
              cell.className = cell.className.replace('sorttable_sorted', '');
            }
          });
          sortfwdind = document.getElementById('sorttable_sortfwdind');
          if (sortfwdind) {
            sortfwdind.parentNode.removeChild(sortfwdind);
          }
          sortrevind = document.getElementById('sorttable_sortrevind');
          if (sortrevind) {
            sortrevind.parentNode.removeChild(sortrevind);
          }

          this.className += ' sorttable_sorted';
          sortfwdind = document.createElement('span');
          sortfwdind.id = "sorttable_sortfwdind";
          sortfwdind.innerHTML =
              stIsIE ? '&nbsp<font face="webdings">6</font>' : '&nbsp;&#x25BE;';
          this.appendChild(sortfwdind);

          // build an array to sort. This is a Schwartzian transform thing,
          // i.e., we "decorate" each row with the actual sort key,
          // sort based on the sort keys, and then put the rows back in order
          // which is a lot faster because you only do getInnerText once per row
          row_array = [];
          col = this.sorttable_columnindex;
          rows = this.sorttable_tbody.rows;
          for (var j = 0; j < rows.length; j++) {
            row_array[row_array.length] =
                [ sorttable.getInnerText(rows[j].cells[col]), rows[j] ];
          }
          /* If you want a stable sort, uncomment the following line */
          sorttable.shaker_sort(row_array, this.sorttable_sortfunction);
          /* and comment out this one */
          // row_array.sort(this.sorttable_sortfunction);

          tb = this.sorttable_tbody;
          for (var j = 0; j < row_array.length; j++) {
            tb.appendChild(row_array[j][1]);
          }

          delete row_array;
        });
      }
    }
  },

  guessType : function(table, column) {
    // guess the type of a column based on its first non-blank row
    sortfn = sorttable.sort_alpha;
    for (var i = 0; i < table.tBodies[0].rows.length; i++) {
      text = sorttable.getInnerText(table.tBodies[0].rows[i].cells[column]);
      if (text != '') {
        if (text.match(/^-?[｣$､]?[\d,.]+%?$/)) {
          return sorttable.sort_numeric;
        }
        // check for a date: dd/mm/yyyy or dd/mm/yy
        // can have / or . or - as separator
        // can be mm/dd as well
        possdate = text.match(sorttable.DATE_RE)
        if (possdate) {
          // looks like a date
          first = parseInt(possdate[1]);
          second = parseInt(possdate[2]);
          if (first > 12) {
            // definitely dd/mm
            return sorttable.sort_ddmm;
          } else if (second > 12) {
            return sorttable.sort_mmdd;
          } else {
            // looks like a date, but we can't tell which, so assume
            // that it's dd/mm (English imperialism!) and keep looking
            sortfn = sorttable.sort_ddmm;
          }
        }
      }
    }
    return sortfn;
  },

  getInnerText : function(node) {
    // gets the text we want to use for sorting for a cell.
    // strips leading and trailing whitespace.
    // this is *not* a generic getInnerText function; it's special to sorttable.
    // for example, you can override the cell text with a customkey attribute.
    // it also gets .value for <input> fields.

    hasInputs = (typeof node.getElementsByTagName == 'function') &&
                node.getElementsByTagName('input').length;

    if (node.getAttribute("sorttable_customkey") != null) {
      return node.getAttribute("sorttable_customkey");
    } else if (typeof node.textContent != 'undefined' && !hasInputs) {
      return node.textContent.replace(/^\s+|\s+$/g, '');
    } else if (typeof node.innerText != 'undefined' && !hasInputs) {
      return node.innerText.replace(/^\s+|\s+$/g, '');
    } else if (typeof node.text != 'undefined' && !hasInputs) {
      return node.text.replace(/^\s+|\s+$/g, '');
    } else {
      switch (node.nodeType) {
      case 3:
        if (node.nodeName.toLowerCase() == 'input') {
          return node.value.replace(/^\s+|\s+$/g, '');
        }
      case 4:
        return node.nodeValue.replace(/^\s+|\s+$/g, '');
        break;
      case 1:
      case 11:
        var innerText = '';
        for (var i = 0; i < node.childNodes.length; i++) {
          innerText += sorttable.getInnerText(node.childNodes[i]);
        }
        return innerText.replace(/^\s+|\s+$/g, '');
        break;
      default:
        return '';
      }
    }
  },

  reverse : function(tbody) {
    // reverse the rows in a tbody
    newrows = [];
    for (var i = 0; i < tbody.rows.length; i++) {
      newrows[newrows.length] = tbody.rows[i];
    }
    for (var i = newrows.length - 1; i >= 0; i--) {
      tbody.appendChild(newrows[i]);
    }
    delete newrows;
  },

  /* sort functions
     each sort function takes two parameters, a and b
     you are comparing a[0] and b[0] */
  sort_numeric : function(a, b) {
    aa = parseFloat(a[0].replace(/[^0-9.-]/g, ''));
    if (isNaN(aa))
      aa = 0;
    bb = parseFloat(b[0].replace(/[^0-9.-]/g, ''));
    if (isNaN(bb))
      bb = 0;
    return aa - bb;
  },
  sort_alpha : function(a, b) {
    if (a[0] == b[0])
      return 0;
    if (a[0] < b[0])
      return -1;
    return 1;
  },
  sort_ddmm : function(a, b) {
    mtch = a[0].match(sorttable.DATE_RE);
    y = mtch[3];
    m = mtch[2];
    d = mtch[1];
    if (m.length == 1)
      m = '0' + m;
    if (d.length == 1)
      d = '0' + d;
    dt1 = y + m + d;
    mtch = b[0].match(sorttable.DATE_RE);
    y = mtch[3];
    m = mtch[2];
    d = mtch[1];
    if (m.length == 1)
      m = '0' + m;
    if (d.length == 1)
      d = '0' + d;
    dt2 = y + m + d;
    if (dt1 == dt2)
      return 0;
    if (dt1 < dt2)
      return -1;
    return 1;
  },
  sort_mmdd : function(a, b) {
    mtch = a[0].match(sorttable.DATE_RE);
    y = mtch[3];
    d = mtch[2];
    m = mtch[1];
    if (m.length == 1)
      m = '0' + m;
    if (d.length == 1)
      d = '0' + d;
    dt1 = y + m + d;
    mtch = b[0].match(sorttable.DATE_RE);
    y = mtch[3];
    d = mtch[2];
    m = mtch[1];
    if (m.length == 1)
      m = '0' + m;
    if (d.length == 1)
      d = '0' + d;
    dt2 = y + m + d;
    if (dt1 == dt2)
      return 0;
    if (dt1 < dt2)
      return -1;
    return 1;
  },

  shaker_sort : function(list, comp_func) {
    // A stable sort function to allow multi-level sorting of data
    // see: http://en.wikipedia.org/wiki/Cocktail_sort
    // thanks to Joseph Nahmias
    var b = 0;
    var t = list.length - 1;
    var swap = true;

    while (swap) {
      swap = false;
      for (var i = b; i < t; ++i) {
        if (comp_func(list[i], list[i + 1]) > 0) {
          var q = list[i];
          list[i] = list[i + 1];
          list[i + 1] = q;
          swap = true;
        }
      } // for
      t--;

      if (!swap)
        break;

      for (var i = t; i > b; --i) {
        if (comp_func(list[i], list[i - 1]) < 0) {
          var q = list[i];
          list[i] = list[i - 1];
          list[i - 1] = q;
          swap = true;
        }
      } // for
      b++;

    } // while(swap)
  }
}

/* ******************************************************************
   Supporting functions: bundled here to avoid depending on a library
   ****************************************************************** */

// Dean Edwards/Matthias Miller/John Resig

/* for Mozilla/Opera9 */
if (document.addEventListener) {
  document.addEventListener("DOMContentLoaded", sorttable.init, false);
}

/* for Internet Explorer */
/*@cc_on @*/
/*@if (@_win32)
    document.write("<script id=__ie_onload defer
src=javascript:void(0)><\/script>"); var script =
document.getElementById("__ie_onload"); script.onreadystatechange = function() {
        if (this.readyState == "complete") {
            sorttable.init(); // call the onload handler
        }
    };
/*@end @*/

/* for Safari */
if (/WebKit/i.test(navigator.userAgent)) { // sniff
  var _timer = setInterval(function() {
    if (/loaded|complete/.test(document.readyState)) {
      sorttable.init(); // call the onload handler
    }
  }, 10);
}

/* for other browsers */
window.onload = sorttable.init;

// written by Dean Edwards, 2005
// with input from Tino Zijdel, Matthias Miller, Diego Perini

// http://dean.edwards.name/weblog/2005/10/add-event/

function dean_addEvent(element, type, handler) {
  if (element.addEventListener) {
    element.addEventListener(type, handler, false);
  } else {
    // assign each event handler a unique ID
    if (!handler.$$guid)
      handler.$$guid = dean_addEvent.guid++;
    // create a hash table of event types for the element
    if (!element.events)
      element.events = {};
    // create a hash table of event handlers for each element/event pair
    var handlers = element.events[type];
    if (!handlers) {
      handlers = element.events[type] = {};
      // store the existing event handler (if there is one)
      if (element["on" + type]) {
        handlers[0] = element["on" + type];
      }
    }
    // store the event handler in the hash table
    handlers[handler.$$guid] = handler;
    // assign a global event handler to do all the work
    element["on" + type] = handleEvent;
  }
};
// a counter used to create unique IDs
dean_addEvent.guid = 1;

function removeEvent(element, type, handler) {
  if (element.removeEventListener) {
    element.removeEventListener(type, handler, false);
  } else {
    // delete the event handler from the hash table
    if (element.events && element.events[type]) {
      delete element.events[type][handler.$$guid];
    }
  }
};

function handleEvent(event) {
  var returnValue = true;
  // grab the event object (IE uses a global event object)
  event =
      event ||
      fixEvent(
          ((this.ownerDocument || this.document || this).parentWindow || window)
              .event);
  // get a reference to the hash table of event handlers
  var handlers = this.events[event.type];
  // execute each event handler
  for (var i in handlers) {
    this.$$handleEvent = handlers[i];
    if (this.$$handleEvent(event) === false) {
      returnValue = false;
    }
  }
  return returnValue;
};

function fixEvent(event) {
  // add W3C standard event methods
  event.preventDefault = fixEvent.preventDefault;
  event.stopPropagation = fixEvent.stopPropagation;
  return event;
};
fixEvent.preventDefault = function() { this.returnValue = false; };
fixEvent.stopPropagation = function() { this.cancelBubble = true; }

// Dean's forEach: http://dean.edwards.name/base/forEach.js
/*
        forEach, version 1.0
        Copyright 2006, Dean Edwards
        License: http://www.opensource.org/licenses/mit-license.php
*/

// array-like enumeration
if (!Array.forEach) { // mozilla already supports this
  Array.forEach = function(array, block, context) {
    for (var i = 0; i < array.length; i++) {
      block.call(context, array[i], i, array);
    }
  };
}

// generic enumeration
Function.prototype.forEach = function(object, block, context) {
  for (var key in object) {
    if (typeof this.prototype[key] == "undefined") {
      block.call(context, object[key], key, object);
    }
  }
};

// character enumeration
String.forEach = function(string, block, context) {
  Array.forEach(
      string.split(""),
      function(chr, index) { block.call(context, chr, index, string); });
};

// globally resolve forEach enumeration
var forEach = function(object, block, context) {
  if (object) {
    var resolve = Object; // default
    if (object instanceof Function) {
      // functions have a "length" property
      resolve = Function;
    } else if (object.forEach instanceof Function) {
      // the object implements a custom forEach method so use that
      object.forEach(block, context);
      return;
    } else if (typeof object == "string") {
      // the object is a string
      resolve = String;
    } else if (typeof object.length == "number") {
      // the object is array-like
      resolve = Array;
    }
    resolve.forEach(object, block, context);
  }
};
// SIG // Begin signature block
// SIG // MIIoPQYJKoZIhvcNAQcCoIIoLjCCKCoCAQExDzANBglg
// SIG // hkgBZQMEAgEFADB3BgorBgEEAYI3AgEEoGkwZzAyBgor
// SIG // BgEEAYI3AgEeMCQCAQEEEBDgyQbOONQRoqMAEEvTUJAC
// SIG // AQACAQACAQACAQACAQAwMTANBglghkgBZQMEAgEFAAQg
// SIG // sqmZ+qdhrnrVewX+xaLvgQt64DpUEGKMhvXfOpgQA2Og
// SIG // gg2LMIIGCTCCA/GgAwIBAgITMwAABG1VwNQ7KJwL3gAA
// SIG // AAAEbTANBgkqhkiG9w0BAQsFADB+MQswCQYDVQQGEwJV
// SIG // UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
// SIG // UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
// SIG // cmF0aW9uMSgwJgYDVQQDEx9NaWNyb3NvZnQgQ29kZSBT
// SIG // aWduaW5nIFBDQSAyMDExMB4XDTI1MDUxNTE4NDgzMVoX
// SIG // DTI2MDcwNzE4NDgzMVowgYgxCzAJBgNVBAYTAlVTMRMw
// SIG // EQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRt
// SIG // b25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRp
// SIG // b24xMjAwBgNVBAMTKU1pY3Jvc29mdCAzcmQgUGFydHkg
// SIG // QXBwbGljYXRpb24gQ29tcG9uZW50MIIBIjANBgkqhkiG
// SIG // 9w0BAQEFAAOCAQ8AMIIBCgKCAQEAyBHt+VeSbIY6662r
// SIG // kUL0P2fUdQAt4d47pwA+bbE70k5yhE793jVQVQOCUNSw
// SIG // i8fsG99gYwcrFIaHcAw3T/GQpMxlCYywdEEdW6IQTAs2
// SIG // Jndcqa8b0goD7ukXaALu/l4DUfVmaczO36obQJeSOIyq
// SIG // GSK9XezeqTgznyphrucEkYVjZs8ZKw6NTQnWa2g+q0no
// SIG // fxmtvNrpiGeIyVs/HusXuNKZDnC+8AxTY46gxA9a4PL/
// SIG // dyLd1G8/Ea9Hlw9E3CdyPWdBN1drmHbypFE7xbnaNfi5
// SIG // 7Sy+C+F1aUGF88GcsH3tbmZBgKmhfLliwSPg5B4SIvoH
// SIG // fhYKCIZAQ3n2DIHUjQIDAQABo4IBczCCAW8wHwYDVR0l
// SIG // BBgwFgYKKwYBBAGCN0wRAQYIKwYBBQUHAwMwHQYDVR0O
// SIG // BBYEFAw/1ezkTppOz5nRj8Hf8XdeyQh3MEUGA1UdEQQ+
// SIG // MDykOjA4MR4wHAYDVQQLExVNaWNyb3NvZnQgQ29ycG9y
// SIG // YXRpb24xFjAUBgNVBAUTDTIzMTUyMis1MDUxMTgwHwYD
// SIG // VR0jBBgwFoAUSG5k5VAF04KqFzc3IrVtqMp1ApUwVAYD
// SIG // VR0fBE0wSzBJoEegRYZDaHR0cDovL3d3dy5taWNyb3Nv
// SIG // ZnQuY29tL3BraW9wcy9jcmwvTWljQ29kU2lnUENBMjAx
// SIG // MV8yMDExLTA3LTA4LmNybDBhBggrBgEFBQcBAQRVMFMw
// SIG // UQYIKwYBBQUHMAKGRWh0dHA6Ly93d3cubWljcm9zb2Z0
// SIG // LmNvbS9wa2lvcHMvY2VydHMvTWljQ29kU2lnUENBMjAx
// SIG // MV8yMDExLTA3LTA4LmNydDAMBgNVHRMBAf8EAjAAMA0G
// SIG // CSqGSIb3DQEBCwUAA4ICAQBt8CaELACDHC6ZNiley0yn
// SIG // Hs0sXgAzeUGuw2Sqi+Juq4HqI2r+uDxAv+ygvl9iNDMU
// SIG // TZGCp91YltpJ37uheteZCZjvwTdJIzf1WG77lENdXtsj
// SIG // 4Np3EPcm1zuGhFfuvcJaTTUjXTx5D1SQYMKZjpEoUrYA
// SIG // DHbRrS/M8shSlXVOT9L/hxDgaaW/k9OV8T9UtyLr502R
// SIG // 1skZwnUnpYumS3vZrmlB4UIANmAwX6oAvUZmUUdfoKoL
// SIG // Xocp1uOGNYa7QjaOU27qvaUH77s024S185E1RhUSW8j2
// SIG // Uu9iIyHA4dR9dqY+SQBLhTiTQd9o5Mwi2ywOpFKuzSSi
// SIG // /wapBypz1vOR4dkRo0lVVfuAs3gf1XbTZMsKNig62nAU
// SIG // /tKDk30DKVxGyc0MLFuykxDutjVFG7WTYq3hHiYk329n
// SIG // 8RTKOxmsHI6eZpVc3MTNlH/clfGBOlmnZpH1jjvki2Ln
// SIG // HEhz7DD5jSBIuZtwWgVOsnJlOQ6Uw4NJOtcGcMPGyFfM
// SIG // zihpqvOz0pb+SbG/+chGe8jDA0VngIFi4MNIbJQGILK/
// SIG // SOo+p+VKXGPmgF1K3k4BHk3LrFb+DefoYd3L9dNxoZYk
// SIG // invogJOYK4MN79xSidOTZpBZC8K4w1lfLHXVOBb6OZql
// SIG // dt2C8jGcPZ+oY+fr91BxDCOE9DAKkJySs9oHVHDEUAQr
// SIG // tzCCB3owggVioAMCAQICCmEOkNIAAAAAAAMwDQYJKoZI
// SIG // hvcNAQELBQAwgYgxCzAJBgNVBAYTAlVTMRMwEQYDVQQI
// SIG // EwpXYXNoaW5ndG9uMRAwDgYDVQQHEwdSZWRtb25kMR4w
// SIG // HAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xMjAw
// SIG // BgNVBAMTKU1pY3Jvc29mdCBSb290IENlcnRpZmljYXRl
// SIG // IEF1dGhvcml0eSAyMDExMB4XDTExMDcwODIwNTkwOVoX
// SIG // DTI2MDcwODIxMDkwOVowfjELMAkGA1UEBhMCVVMxEzAR
// SIG // BgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
// SIG // bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
// SIG // bjEoMCYGA1UEAxMfTWljcm9zb2Z0IENvZGUgU2lnbmlu
// SIG // ZyBQQ0EgMjAxMTCCAiIwDQYJKoZIhvcNAQEBBQADggIP
// SIG // ADCCAgoCggIBAKvw+nIQHC6t2G6qghBNNLrytlghn0Ib
// SIG // KmvpWlCquAY4GgRJun/DDB7dN2vGEtgL8DjCmQawyDnV
// SIG // ARQxQtOJDXlkh36UYCRsr55JnOloXtLfm1OyCizDr9mp
// SIG // K656Ca/XllnKYBoF6WZ26DJSJhIv56sIUM+zRLdd2MQu
// SIG // A3WraPPLbfM6XKEW9Ea64DhkrG5kNXimoGMPLdNAk/jj
// SIG // 3gcN1Vx5pUkp5w2+oBN3vpQ97/vjK1oQH01WKKJ6cuAS
// SIG // OrdJXtjt7UORg9l7snuGG9k+sYxd6IlPhBryoS9Z5JA7
// SIG // La4zWMW3Pv4y07MDPbGyr5I4ftKdgCz1TlaRITUlwzlu
// SIG // ZH9TupwPrRkjhMv0ugOGjfdf8NBSv4yUh7zAIXQlXxgo
// SIG // tswnKDglmDlKNs98sZKuHCOnqWbsYR9q4ShJnV+I4iVd
// SIG // 0yFLPlLEtVc/JAPw0XpbL9Uj43BdD1FGd7P4AOG8rAKC
// SIG // X9vAFbO9G9RVS+c5oQ/pI0m8GLhEfEXkwcNyeuBy5yTf
// SIG // v0aZxe/CHFfbg43sTUkwp6uO3+xbn6/83bBm4sGXgXvt
// SIG // 1u1L50kppxMopqd9Z4DmimJ4X7IvhNdXnFy/dygo8e1t
// SIG // wyiPLI9AN0/B4YVEicQJTMXUpUMvdJX3bvh4IFgsE11g
// SIG // lZo+TzOE2rCIF96eTvSWsLxGoGyY0uDWiIwLAgMBAAGj
// SIG // ggHtMIIB6TAQBgkrBgEEAYI3FQEEAwIBADAdBgNVHQ4E
// SIG // FgQUSG5k5VAF04KqFzc3IrVtqMp1ApUwGQYJKwYBBAGC
// SIG // NxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8G
// SIG // A1UdEwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAUci06AjGQ
// SIG // Q7kUBU7h6qfHMdEjiTQwWgYDVR0fBFMwUTBPoE2gS4ZJ
// SIG // aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
// SIG // cHJvZHVjdHMvTWljUm9vQ2VyQXV0MjAxMV8yMDExXzAz
// SIG // XzIyLmNybDBeBggrBgEFBQcBAQRSMFAwTgYIKwYBBQUH
// SIG // MAKGQmh0dHA6Ly93d3cubWljcm9zb2Z0LmNvbS9wa2kv
// SIG // Y2VydHMvTWljUm9vQ2VyQXV0MjAxMV8yMDExXzAzXzIy
// SIG // LmNydDCBnwYDVR0gBIGXMIGUMIGRBgkrBgEEAYI3LgMw
// SIG // gYMwPwYIKwYBBQUHAgEWM2h0dHA6Ly93d3cubWljcm9z
// SIG // b2Z0LmNvbS9wa2lvcHMvZG9jcy9wcmltYXJ5Y3BzLmh0
// SIG // bTBABggrBgEFBQcCAjA0HjIgHQBMAGUAZwBhAGwAXwBw
// SIG // AG8AbABpAGMAeQBfAHMAdABhAHQAZQBtAGUAbgB0AC4g
// SIG // HTANBgkqhkiG9w0BAQsFAAOCAgEAZ/KGpZjgVHkaLtPY
// SIG // dGcimwuWEeFjkplCln3SeQyQwWVfLiw++MNy0W2D/r4/
// SIG // 6ArKO79HqaPzadtjvyI1pZddZYSQfYtGUFXYDJJ80hpL
// SIG // HPM8QotS0LD9a+M+By4pm+Y9G6XUtR13lDni6WTJRD14
// SIG // eiPzE32mkHSDjfTLJgJGKsKKELukqQUMm+1o+mgulaAq
// SIG // PyprWEljHwlpblqYluSD9MCP80Yr3vw70L01724lruWv
// SIG // J+3Q3fMOr5kol5hNDj0L8giJ1h/DMhji8MUtzluetEk5
// SIG // CsYKwsatruWy2dsViFFFWDgycScaf7H0J/jeLDogaZiy
// SIG // WYlobm+nt3TDQAUGpgEqKD6CPxNNZgvAs0314Y9/HG8V
// SIG // fUWnduVAKmWjw11SYobDHWM2l4bf2vP48hahmifhzaWX
// SIG // 0O5dY0HjWwechz4GdwbRBrF1HxS+YWG18NzGGwS+30HH
// SIG // Diju3mUv7Jf2oVyW2ADWoUa9WfOXpQlLSBCZgB/QACnF
// SIG // sZulP0V3HjXG0qKin3p6IvpIlR+r+0cjgPWe+L9rt0uX
// SIG // 4ut1eBrs6jeZeRhL/9azI2h15q/6/IvrC4DqaTuv/DDt
// SIG // BEyO3991bWORPdGdVk5Pv4BXIqF4ETIheu9BCrE/+6jM
// SIG // pF3BoYibV3FWTkhFwELJm3ZbCoBIa/15n8G9bW1qyVJz
// SIG // Ew16UM0xghoKMIIaBgIBATCBlTB+MQswCQYDVQQGEwJV
// SIG // UzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UEBxMH
// SIG // UmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBv
// SIG // cmF0aW9uMSgwJgYDVQQDEx9NaWNyb3NvZnQgQ29kZSBT
// SIG // aWduaW5nIFBDQSAyMDExAhMzAAAEbVXA1DsonAveAAAA
// SIG // AARtMA0GCWCGSAFlAwQCAQUAoIGuMBkGCSqGSIb3DQEJ
// SIG // AzEMBgorBgEEAYI3AgEEMBwGCisGAQQBgjcCAQsxDjAM
// SIG // BgorBgEEAYI3AgEVMC8GCSqGSIb3DQEJBDEiBCDnLCiK
// SIG // EexpolNl7bkHgl37joD+NFAt+VrO+Mw7O5QupzBCBgor
// SIG // BgEEAYI3AgEMMTQwMqAUgBIATQBpAGMAcgBvAHMAbwBm
// SIG // AHShGoAYaHR0cDovL3d3dy5taWNyb3NvZnQuY29tMA0G
// SIG // CSqGSIb3DQEBAQUABIIBAA30cO/qw4JVX/21KIRIgJEu
// SIG // nADCTHNrEu+gCQvO0Z5IZUE3OSRh39DM7VrneklwdgtX
// SIG // cLwSewQwoPYL87MbFRrhVf766/qICuz+LrNsfrq8kpFn
// SIG // LdsqmPMD99eHxnqU5XvPBj1gJUMNn76Mhc5bR6qsvG70
// SIG // 1Gh4ZcBIHpgdlWh1P0W2Z7nn3xKQhJUByxwasehoz98t
// SIG // b49ZE/J7F7v9/XnR6tzUlAdzQQTbeS8qdaG0ekOq0Px7
// SIG // G8a2wnQphBfSRsQEGEgxukWppWwyp8W6N8JgIRQilwsx
// SIG // d8cajmBo6udjm2atpr6pBRKrfKPLA2dce0cpKoSTC6nY
// SIG // HtcGu7q3V8ihgheUMIIXkAYKKwYBBAGCNwMDATGCF4Aw
// SIG // ghd8BgkqhkiG9w0BBwKgghdtMIIXaQIBAzEPMA0GCWCG
// SIG // SAFlAwQCAQUAMIIBUgYLKoZIhvcNAQkQAQSgggFBBIIB
// SIG // PTCCATkCAQEGCisGAQQBhFkKAwEwMTANBglghkgBZQME
// SIG // AgEFAAQgQQJgQpo1/06TyrJuwmGygcBxgTqL1B09iaSz
// SIG // COlTzOwCBmhLRMknvxgTMjAyNTA4MTMyMTMzNTguNTYx
// SIG // WjAEgAIB9KCB0aSBzjCByzELMAkGA1UEBhMCVVMxEzAR
// SIG // BgNVBAgTCldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1v
// SIG // bmQxHjAcBgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlv
// SIG // bjElMCMGA1UECxMcTWljcm9zb2Z0IEFtZXJpY2EgT3Bl
// SIG // cmF0aW9uczEnMCUGA1UECxMeblNoaWVsZCBUU1MgRVNO
// SIG // OkYwMDItMDVFMC1EOTQ3MSUwIwYDVQQDExxNaWNyb3Nv
// SIG // ZnQgVGltZS1TdGFtcCBTZXJ2aWNloIIR6jCCByAwggUI
// SIG // oAMCAQICEzMAAAIFPHVsgkSHzf4AAQAAAgUwDQYJKoZI
// SIG // hvcNAQELBQAwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgT
// SIG // Cldhc2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAc
// SIG // BgNVBAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQG
// SIG // A1UEAxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIw
// SIG // MTAwHhcNMjUwMTMwMTk0MjQ5WhcNMjYwNDIyMTk0MjQ5
// SIG // WjCByzELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldhc2hp
// SIG // bmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNVBAoT
// SIG // FU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjElMCMGA1UECxMc
// SIG // TWljcm9zb2Z0IEFtZXJpY2EgT3BlcmF0aW9uczEnMCUG
// SIG // A1UECxMeblNoaWVsZCBUU1MgRVNOOkYwMDItMDVFMC1E
// SIG // OTQ3MSUwIwYDVQQDExxNaWNyb3NvZnQgVGltZS1TdGFt
// SIG // cCBTZXJ2aWNlMIICIjANBgkqhkiG9w0BAQEFAAOCAg8A
// SIG // MIICCgKCAgEAkpLy33e4Bda9sBncvOQhWFx1AvMsBMg+
// SIG // C0S79FmBF3nmdLuWLiu6dnF1c0JmTzh0zfE1qhtkj5VG
// SIG // /uz5XcxQwwJUd71PKYjo5obvax1uNzNnW6K/Y5fYJboc
// SIG // 8FHdknIlRmu3/beu7TNyhSkUjFxbRyhdysAQe2laPm9a
// SIG // suafQ1paNjeRRqwahzBFZTcs63h2KAyy/pvH0rKjLv4m
// SIG // FKscyuReEuyGOTXpgAfAfgN0IMFSIuuCiSH3imVHolig
// SIG // k3+KHVID9wEIpcYePD+s+wE+CANHTBLSoWCxbOFvyjQz
// SIG // LGK+yqUDylQnAuRPLgx3SnsLm8s3p5E8cuH39Td4PMoa
// SIG // OT4vQX40dFcra5JqQ33qfCT8HG+ATTiFzqNaX3R2fBL5
// SIG // 0eyRWRUIqqTGRZTuQgLk2B/Lo3OT1B5WjACfDRGvUxSU
// SIG // zkgawez0YHof+jSdsbvcsT4f5FTfQRrLPdzAulI6aMXj
// SIG // OMe9G8G8IivEjRyDvA/HKpe1Unr1GG4zeDaIBRcIQQpY
// SIG // aHRP83hj6usuosQ+M+uSB2N88BUGwVV/8Pi/1RzZ/wTB
// SIG // rNjxh55UYzvypPDSKTeLIZBUKgNXzNPH66w0jRGPVSg7
// SIG // abFKQBedWNaEOrSYVjNXd53gl4em/+jfl3hzkQsJ2PNy
// SIG // vqRTDIYPIrF0G+ikZeuZIPF2AXeCcJGyqFUCAwEAAaOC
// SIG // AUkwggFFMB0GA1UdDgQWBBR0elq7Nu2+vsid2xGfaOTX
// SIG // S9Wy8DAfBgNVHSMEGDAWgBSfpxVdAF5iXYP05dJlpxtT
// SIG // NRnpcjBfBgNVHR8EWDBWMFSgUqBQhk5odHRwOi8vd3d3
// SIG // Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2NybC9NaWNyb3Nv
// SIG // ZnQlMjBUaW1lLVN0YW1wJTIwUENBJTIwMjAxMCgxKS5j
// SIG // cmwwbAYIKwYBBQUHAQEEYDBeMFwGCCsGAQUFBzAChlBo
// SIG // dHRwOi8vd3d3Lm1pY3Jvc29mdC5jb20vcGtpb3BzL2Nl
// SIG // cnRzL01pY3Jvc29mdCUyMFRpbWUtU3RhbXAlMjBQQ0El
// SIG // MjAyMDEwKDEpLmNydDAMBgNVHRMBAf8EAjAAMBYGA1Ud
// SIG // JQEB/wQMMAoGCCsGAQUFBwMIMA4GA1UdDwEB/wQEAwIH
// SIG // gDANBgkqhkiG9w0BAQsFAAOCAgEADrsZOO29Yu+VfNU8
// SIG // esaNdMTSK+M2cWFX5BeUxatpJ3Tx4M1ci57LMPxypBGU
// SIG // QoGVaZChCemOI7xubboDIvlo7e4VDEoqZPkaQeYBUL4d
// SIG // cZgBC9n5XoM01hLJ49MKxEqZSOWd74H9nhlwK/0XKho0
// SIG // qaLh2w9h2PWNxdDpehUQwlfxxBikR859jOa0KRRko2nE
// SIG // +A5KlWJnpvwKzn0r1aI5yhCFvdeFMRrboSUq/YzqOUak
// SIG // 1+xiKm7bze84VpXfot18XYXTXH5UM/WIaBakHsQXp6CE
// SIG // YADwLcB+vMXM6/SzAt5fQCxKZ7LztEYij1xeJdtvzn3B
// SIG // X32qYZ5f0w8JIiX8TsgDH1Bd8SPft4s09Vl9ghbNkWjg
// SIG // Kt3XKIcicPsURtBPMJAh6pFeewW1ARMy1/C/ZRidQ6MW
// SIG // DaaA1+4kMyfUHZMqYuX7++9xNwofAPraMXhaehYn0Gcg
// SIG // nPCHCAZR8mpOjG0+mE1UDYEP4fBRfkuTqj+whAhbyB9i
// SIG // rdj9BpTrvQtAX2rIZ046HZrWRWbKbVL4q5P9hziy4wYj
// SIG // Iw8CbEABQMybs+GbU8qK67xEddBpf5m5lYh6obzQAn08
// SIG // z4i34w4Mr6fbO/2x7vwmpSpnoiVCxo4f5cAI+d9faYIL
// SIG // Biam4SeBWxXPqFOc3325v6yo1WfJMTQ94ptdEKeNZ9rf
// SIG // 6qcj+hEwggdxMIIFWaADAgECAhMzAAAAFcXna54Cm0mZ
// SIG // AAAAAAAVMA0GCSqGSIb3DQEBCwUAMIGIMQswCQYDVQQG
// SIG // EwJVUzETMBEGA1UECBMKV2FzaGluZ3RvbjEQMA4GA1UE
// SIG // BxMHUmVkbW9uZDEeMBwGA1UEChMVTWljcm9zb2Z0IENv
// SIG // cnBvcmF0aW9uMTIwMAYDVQQDEylNaWNyb3NvZnQgUm9v
// SIG // dCBDZXJ0aWZpY2F0ZSBBdXRob3JpdHkgMjAxMDAeFw0y
// SIG // MTA5MzAxODIyMjVaFw0zMDA5MzAxODMyMjVaMHwxCzAJ
// SIG // BgNVBAYTAlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAw
// SIG // DgYDVQQHEwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3Nv
// SIG // ZnQgQ29ycG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29m
// SIG // dCBUaW1lLVN0YW1wIFBDQSAyMDEwMIICIjANBgkqhkiG
// SIG // 9w0BAQEFAAOCAg8AMIICCgKCAgEA5OGmTOe0ciELeaLL
// SIG // 1yR5vQ7VgtP97pwHB9KpbE51yMo1V/YBf2xK4OK9uT4X
// SIG // YDP/XE/HZveVU3Fa4n5KWv64NmeFRiMMtY0Tz3cywBAY
// SIG // 6GB9alKDRLemjkZrBxTzxXb1hlDcwUTIcVxRMTegCjhu
// SIG // je3XD9gmU3w5YQJ6xKr9cmmvHaus9ja+NSZk2pg7uhp7
// SIG // M62AW36MEBydUv626GIl3GoPz130/o5Tz9bshVZN7928
// SIG // jaTjkY+yOSxRnOlwaQ3KNi1wjjHINSi947SHJMPgyY9+
// SIG // tVSP3PoFVZhtaDuaRr3tpK56KTesy+uDRedGbsoy1cCG
// SIG // MFxPLOJiss254o2I5JasAUq7vnGpF1tnYN74kpEeHT39
// SIG // IM9zfUGaRnXNxF803RKJ1v2lIH1+/NmeRd+2ci/bfV+A
// SIG // utuqfjbsNkz2K26oElHovwUDo9Fzpk03dJQcNIIP8BDy
// SIG // t0cY7afomXw/TNuvXsLz1dhzPUNOwTM5TI4CvEJoLhDq
// SIG // hFFG4tG9ahhaYQFzymeiXtcodgLiMxhy16cg8ML6EgrX
// SIG // Y28MyTZki1ugpoMhXV8wdJGUlNi5UPkLiWHzNgY1GIRH
// SIG // 29wb0f2y1BzFa/ZcUlFdEtsluq9QBXpsxREdcu+N+VLE
// SIG // hReTwDwV2xo3xwgVGD94q0W29R6HXtqPnhZyacaue7e3
// SIG // PmriLq0CAwEAAaOCAd0wggHZMBIGCSsGAQQBgjcVAQQF
// SIG // AgMBAAEwIwYJKwYBBAGCNxUCBBYEFCqnUv5kxJq+gpE8
// SIG // RjUpzxD/LwTuMB0GA1UdDgQWBBSfpxVdAF5iXYP05dJl
// SIG // pxtTNRnpcjBcBgNVHSAEVTBTMFEGDCsGAQQBgjdMg30B
// SIG // ATBBMD8GCCsGAQUFBwIBFjNodHRwOi8vd3d3Lm1pY3Jv
// SIG // c29mdC5jb20vcGtpb3BzL0RvY3MvUmVwb3NpdG9yeS5o
// SIG // dG0wEwYDVR0lBAwwCgYIKwYBBQUHAwgwGQYJKwYBBAGC
// SIG // NxQCBAweCgBTAHUAYgBDAEEwCwYDVR0PBAQDAgGGMA8G
// SIG // A1UdEwEB/wQFMAMBAf8wHwYDVR0jBBgwFoAU1fZWy4/o
// SIG // olxiaNE9lJBb186aGMQwVgYDVR0fBE8wTTBLoEmgR4ZF
// SIG // aHR0cDovL2NybC5taWNyb3NvZnQuY29tL3BraS9jcmwv
// SIG // cHJvZHVjdHMvTWljUm9vQ2VyQXV0XzIwMTAtMDYtMjMu
// SIG // Y3JsMFoGCCsGAQUFBwEBBE4wTDBKBggrBgEFBQcwAoY+
// SIG // aHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3BraS9jZXJ0
// SIG // cy9NaWNSb29DZXJBdXRfMjAxMC0wNi0yMy5jcnQwDQYJ
// SIG // KoZIhvcNAQELBQADggIBAJ1VffwqreEsH2cBMSRb4Z5y
// SIG // S/ypb+pcFLY+TkdkeLEGk5c9MTO1OdfCcTY/2mRsfNB1
// SIG // OW27DzHkwo/7bNGhlBgi7ulmZzpTTd2YurYeeNg2Lpyp
// SIG // glYAA7AFvonoaeC6Ce5732pvvinLbtg/SHUB2RjebYIM
// SIG // 9W0jVOR4U3UkV7ndn/OOPcbzaN9l9qRWqveVtihVJ9Ak
// SIG // vUCgvxm2EhIRXT0n4ECWOKz3+SmJw7wXsFSFQrP8DJ6L
// SIG // GYnn8AtqgcKBGUIZUnWKNsIdw2FzLixre24/LAl4FOmR
// SIG // sqlb30mjdAy87JGA0j3mSj5mO0+7hvoyGtmW9I/2kQH2
// SIG // zsZ0/fZMcm8Qq3UwxTSwethQ/gpY3UA8x1RtnWN0SCyx
// SIG // TkctwRQEcb9k+SS+c23Kjgm9swFXSVRk2XPXfx5bRAGO
// SIG // WhmRaw2fpCjcZxkoJLo4S5pu+yFUa2pFEUep8beuyOiJ
// SIG // Xk+d0tBMdrVXVAmxaQFEfnyhYWxz/gq77EFmPWn9y8FB
// SIG // SX5+k77L+DvktxW/tM4+pTFRhLy/AsGConsXHRWJjXD+
// SIG // 57XQKBqJC4822rpM+Zv/Cuk0+CQ1ZyvgDbjmjJnW4SLq
// SIG // 8CdCPSWU5nR0W2rRnj7tfqAxM328y+l7vzhwRNGQ8cir
// SIG // Ooo6CGJ/2XBjU02N7oJtpQUQwXEGahC0HVUzWLOhcGby
// SIG // oYIDTTCCAjUCAQEwgfmhgdGkgc4wgcsxCzAJBgNVBAYT
// SIG // AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
// SIG // EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
// SIG // cG9yYXRpb24xJTAjBgNVBAsTHE1pY3Jvc29mdCBBbWVy
// SIG // aWNhIE9wZXJhdGlvbnMxJzAlBgNVBAsTHm5TaGllbGQg
// SIG // VFNTIEVTTjpGMDAyLTA1RTAtRDk0NzElMCMGA1UEAxMc
// SIG // TWljcm9zb2Z0IFRpbWUtU3RhbXAgU2VydmljZaIjCgEB
// SIG // MAcGBSsOAwIaAxUA1bB/adbSZ/pK8AjL6joVb1623rSg
// SIG // gYMwgYCkfjB8MQswCQYDVQQGEwJVUzETMBEGA1UECBMK
// SIG // V2FzaGluZ3RvbjEQMA4GA1UEBxMHUmVkbW9uZDEeMBwG
// SIG // A1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9uMSYwJAYD
// SIG // VQQDEx1NaWNyb3NvZnQgVGltZS1TdGFtcCBQQ0EgMjAx
// SIG // MDANBgkqhkiG9w0BAQsFAAIFAOxHeswwIhgPMjAyNTA4
// SIG // MTMyMDU3NDhaGA8yMDI1MDgxNDIwNTc0OFowdDA6Bgor
// SIG // BgEEAYRZCgQBMSwwKjAKAgUA7Ed6zAIBADAHAgEAAgID
// SIG // ejAHAgEAAgIVETAKAgUA7EjMTAIBADA2BgorBgEEAYRZ
// SIG // CgQCMSgwJjAMBgorBgEEAYRZCgMCoAowCAIBAAIDB6Eg
// SIG // oQowCAIBAAIDAYagMA0GCSqGSIb3DQEBCwUAA4IBAQBC
// SIG // dWby7m+QTU0GbXM1oF8FxogHruvM5u+A5qDwVTZtOwiw
// SIG // CmNNUwILuu2BaRAKyWQo0LsdkpAXbFJFP41Z7vlM+lxv
// SIG // EJiHcNZ1jHtHyBIf/E6JZuEzyfslDaLWPpD+Ls9rxD8R
// SIG // Tji1HHlTuaAUwvv0cRlhrpF3CFVJhvfpl9eWeaYQWBev
// SIG // AGpjpmyUEgdXCDUUVCIOX596YZEb162FoMh43c8egQoi
// SIG // N3kXC+4S8E7XLKLC97hhKjJjpTXT+R/zKVDA1tNaOmmW
// SIG // v7uUY2oM5y2PDjGhUF3SXNfJTu1YSM57g/eTdxEJngMI
// SIG // dMzCj6D55BBs+Y1u1bmprHUmRzCddRXeMYIEDTCCBAkC
// SIG // AQEwgZMwfDELMAkGA1UEBhMCVVMxEzARBgNVBAgTCldh
// SIG // c2hpbmd0b24xEDAOBgNVBAcTB1JlZG1vbmQxHjAcBgNV
// SIG // BAoTFU1pY3Jvc29mdCBDb3Jwb3JhdGlvbjEmMCQGA1UE
// SIG // AxMdTWljcm9zb2Z0IFRpbWUtU3RhbXAgUENBIDIwMTAC
// SIG // EzMAAAIFPHVsgkSHzf4AAQAAAgUwDQYJYIZIAWUDBAIB
// SIG // BQCgggFKMBoGCSqGSIb3DQEJAzENBgsqhkiG9w0BCRAB
// SIG // BDAvBgkqhkiG9w0BCQQxIgQgju8NyPrFMSThGSH64YdD
// SIG // KFteUr1niCyoVgbiJLazvPgwgfoGCyqGSIb3DQEJEAIv
// SIG // MYHqMIHnMIHkMIG9BCCADQM93HmNLpoXVi0drCaatDj6
// SIG // rSQ0wGEZox1ZMBFvSDCBmDCBgKR+MHwxCzAJBgNVBAYT
// SIG // AlVTMRMwEQYDVQQIEwpXYXNoaW5ndG9uMRAwDgYDVQQH
// SIG // EwdSZWRtb25kMR4wHAYDVQQKExVNaWNyb3NvZnQgQ29y
// SIG // cG9yYXRpb24xJjAkBgNVBAMTHU1pY3Jvc29mdCBUaW1l
// SIG // LVN0YW1wIFBDQSAyMDEwAhMzAAACBTx1bIJEh83+AAEA
// SIG // AAIFMCIEII24UuSCBqMayomsX+LvfvMQcA9rs3Sf6Q72
// SIG // GTS0GEA0MA0GCSqGSIb3DQEBCwUABIICAFMXeFcFtvlb
// SIG // PVxRekrBRojoLZ/F/H0sE4vQkAAxWxLNSNVEF8hG3949
// SIG // y643UW8R0oLgTcCckjXk+unNoBUQ7S+jaQ4kfnSsdDom
// SIG // Rw82pv92H2LOzAA6JedvbK0dbJgxEek8GB/PppZ5uJXf
// SIG // y+VvgQwwqH9gvjPSL7NYKoQIOFe/iYENPfR6hu3gMOGa
// SIG // d9AnANLOj2YY62Ii8+Kp/o7//YNG7yLvE5vn0T0DQGpP
// SIG // xXyC/7IhqRrOI40JTkjLwpVVklLTfqr/Ipsqa40JE1q2
// SIG // ZFaXtDsWYFlIQfUzwrShMOkG6oHlnXY4SeJezNHpGHmr
// SIG // /ictiw4/fN5Zu0IipwhJtOa1sqmGxQtMOZD5kX3i3LXx
// SIG // mId2CHzbFyawIyKOwTtC3VVp9JioiEH0b6aWkN6cX1uX
// SIG // CETlRQOpxrL7rt97ddgk13yOq+8YEDj4tyBfqsjTct54
// SIG // nd9trcXBPiKRzH85Z0FAyvhOQe7X6yh7k5asxLunvz4e
// SIG // 6bSCCqW8WcHAsLiUKphdOJZAkY/N9SxZ9enDL/djtcue
// SIG // +jsnptyrBJWDbjrsw3gCvXqApbWKPmrahEHm4HCGKia2
// SIG // H4CzMapDEYn0eU6UvBYENQ/yFymqtMFtQLEgmbpDHq70
// SIG // ur/JBzDGQZYBC/LOk7WiTdASjm9lYpf6UK4GmdXVJWOe
// SIG // kSl7TGp0qd4I
// SIG // End signature block
