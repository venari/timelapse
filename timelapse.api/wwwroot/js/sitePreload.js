// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
function localizeDateTime(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        document.write(d.toLocaleString());
    } else {
        document.write('---');
    }
}

function localizeDate(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        document.write(d.toLocaleDateString());
    } else {
        document.write('---');
    }
}

// function localizeDate(t)
// {
//     var d=new Date(t);
//     return d.toLocaleString();
// }
