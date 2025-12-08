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
        calendar_analysis = 1,
        calendar_statistics = 2,
        create_event = 3,
        delete_email = 4,
        delete_event = 5,
        forward_email = 6,
        reply_email = 7,
        search_event = 8,
        send_email = 9,
        update_event = 10
    }
}
