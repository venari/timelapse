// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function localizeDateTime(t)
{
    var d=new Date(t+"Z");
    document.write(d.toLocaleString());
}

function localizeDate(t)
{
    var d=new Date(t+"Z");
    document.write(d.toLocaleDateString());
}

// function localizeDate(t)
// {
//     var d=new Date(t);
//     return d.toLocaleString();
// }
