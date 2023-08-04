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

function localizeValidationError(errorJSON){

    var error = JSON.parse(errorJSON);
    
    if(error.StartDateUtc != null && error.EndDateUtc != null){
        localizeDateTime(error.StartDateUtc);
        document.write(' -> ')
        localizeDateTime(error.EndDateUtc);
        document.write(': ' + error.Message)
    
    } else {

        if(error.StartDateUtc != null){
            localizeDateTime(error.StartDateUtc);
            document.write(': ' + error.Message)

        } else {
            if(error.EndDateUtc != null){
                localizeDateTime(error.EndDateUtc);
                document.write(': ' + error.Message)
            } else {
                document.write(error.Message)
            }
        }
    }
}

function localizeDateTimeAsString(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        return d.toLocaleString();
    } else {
        return '---';
    }
}

function localizeDateAsString(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        return d.toLocaleDateString();
    } else {
        return '---';
    }
}

// function localizeDate(t)
// {
//     var d=new Date(t);
//     return d.toLocaleString();
// }


function ISO8601UTCDatetimeToLocalDatetime(ISO8601UTCDatetime)
{
    // I can't fully describe how much I have how JavaScript handles dates. 
    if(ISO8601UTCDatetime.indexOf('Z')==-1){
        ISO8601UTCDatetime+='Z'
    }

    //https://stackoverflow.com/questions/24468518/html5-input-datetime-local-default-value-of-today-and-current-time *@
    var localDatetime = new Date(ISO8601UTCDatetime);
    localDatetime.setMinutes(localDatetime.getMinutes() - localDatetime.getTimezoneOffset());

    /* remove second/millisecond if needed - credit ref. https://stackoverflow.com/questions/24468518/html5-input-datetime-local-default-value-of-today-and-current-time#comment112871765_60884408 */
    localDatetime.setMilliseconds(null)
    // localDatetime.setSeconds(null)

    return localDatetime.toISOString().slice(0, -1);
}