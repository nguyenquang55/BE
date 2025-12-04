using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum Intent
    {
        access_email = 0,
        create_event = 1,
        delete_email = 2,
        delete_event = 3,
        forward_email = 4,
        reply_email = 5,
        search_event = 6,
        send_email = 7,
        update_event = 8
    }
}
