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


function ISO8601UTCDatetimeToLocalDatetime(strISO8601UTCDatetime)
{
    // I can't fully describe how much I have how JavaScript handles dates. 
    if(strISO8601UTCDatetime.indexOf('Z')==-1){
        strISO8601UTCDatetime+='Z'
    }

    //https://stackoverflow.com/questions/24468518/html5-input-datetime-local-default-value-of-today-and-current-time *@
    var localDatetime = new Date(strISO8601UTCDatetime);
    localDatetime.setMinutes(localDatetime.getMinutes() - localDatetime.getTimezoneOffset());

    /* remove second/millisecond if needed - credit ref. https://stackoverflow.com/questions/24468518/html5-input-datetime-local-default-value-of-today-and-current-time#comment112871765_60884408 */
    localDatetime.setMilliseconds(null)
    // localDatetime.setSeconds(null)

    return localDatetime.toISOString().slice(0, -1);
}

function LocalDatetimeToISO8601UTCDatetime(strLocalDatetime)
{
    var ISO8601UTCDatetime = new Date(strLocalDatetime);
    ISO8601UTCDatetime.setMilliseconds(null)
    return ISO8601UTCDatetime.toISOString()
}

const shortDayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']

function localizeDateTimeToShortDayAndNumber(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        return shortDayNames[d.getDay()] + ' ' + d.getDate();
    } else {
        return '---';
    }
}


function localizeDateTimeToHourAndAmPm(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);
        if(d.getHours()<12){
            if(d.getHours()==0){
                return "12AM"
            } else {
                return d.getHours() + "AM"
            }
        } else {
            if(d.getHours()==12){
                return d.getHours() + "PM"
            } else {
                return d.getHours() - 12 + "PM"
            }
        }
        //return d.getHours() + (d.getHours()<12?"AM":"PM");
    } else {
        return '---';
    }
}

function localizeDateTimeIsBetween6And6(t)
{
    if(t && t!='---'){
        if(t.indexOf('Z')==-1){
            t+='Z'
        }
        var d=new Date(t);

        if(d.getHours()>=6 && d.getHours()<18){
            return 'Day';
        } else {
            return 'Night';
        }
    } else {
        return '---';
    }
}

var daytimeColumns = ['Device Name', 'Device Description', 'Device Support Mode', 'Device Monitoring Mode', 'Device Service', 'Device Hibernate Mode', 'Device Power Off'];