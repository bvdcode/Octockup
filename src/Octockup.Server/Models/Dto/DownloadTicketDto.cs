// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class DownloadTicketDto
    {
        public string Ticket { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
