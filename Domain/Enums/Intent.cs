using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum Intent
    {
        check_inbox = 0,
        create_event = 1,
        delete_email = 2,
        delete_event = 3,
        forward_email = 4,
        read_email = 5,
        reply_email = 6,
        search_email = 7,
        search_event = 8,
        send_email = 9,
        update_event = 10
    }
}
