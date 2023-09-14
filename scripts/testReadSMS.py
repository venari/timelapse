from SIM7600X import turnOnNDIS, sendSMS, receiveSMS, deleteAllSMS, powerUpSIM7600X

#try:
#powerUpSIM7600X()
#sendSMS('+64xxxxxxxxx','Testing tesing')
rec_buff = receiveSMS()
print(rec_buff)

rec_lines = rec_buff.splitlines()

for line in rec_lines:
    print(line)
    if(line.startswith("+CMGL:")):
        # Header
        comma_buff = line.split(',')
        message_index = comma_buff[0]
        message_status = comma_buff[1]
        phone_number = comma_buff[2]
        address_text = comma_buff[3]
        timestamp = comma_buff[4]
        print(message_index)
        print(message_status)
        print(phone_number)
        print(timestamp)
    else:
        print(line)

        if line == "STATUS?":
            print("Status query")
        # Body


deleteAllSMS()
