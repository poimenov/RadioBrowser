<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="html" version="1.0" encoding="UTF-8" indent="yes"/>
    <xsl:param name="titleText" select="'History'"/>
    <xsl:param name="startTimeText" select="'Time'"/>
    <xsl:param name="trackNameText" select="'Track Title'"/>
    <xsl:param name="stationText" select="'Station'"/>    
    
    <xsl:template match="/">
        <html>
            <head>
                <title><xsl:value-of select="$titleText"/></title>
                <style>
                    body {
                    font-family: Arial, sans-serif;
                    margin: 20px;
                    background-color: #f5f5f5;
                    }
                    .history-container {
                    max-width: 800px;
                    margin: 0 auto;
                    background-color: white;
                    padding: 20px;
                    border-radius: 8px;
                    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                    }
                    h1, h2 {
                    color: #333;
                    text-align: center;
                    margin-bottom: 20px;
                    }
                    table {
                    width: 100%;
                    border-collapse: collapse;
                    }
                    th, td {
                    padding: 12px;
                    text-align: left;
                    border-bottom: 1px solid #ddd;
                    }
                    th {
                    background-color: #f2f2f2;
                    font-weight: bold;
                    }
                    tr:nth-child(even) {
                    background-color: #f9f9f9;
                    }
                    .time {
                    white-space: nowrap;
                    }
                </style>
            </head>
            <body>
                <div class="history-container">
                    <h1><xsl:value-of select="$titleText"/></h1>
                    <h2><xsl:value-of select="History/@Date"/></h2>
                    <table>
                        <thead>
                            <tr>
                                <th><xsl:value-of select="$startTimeText"/></th>
                                <th><xsl:value-of select="$stationText"/></th>
                                <th><xsl:value-of select="$trackNameText"/></th>                                
                            </tr>
                        </thead>
                        <tbody>
                            <xsl:apply-templates select="History/Record"/>
                        </tbody>
                    </table>
                </div>
            </body>
        </html>
    </xsl:template>
    
    <xsl:template match="Record">
        <tr>
            <td class="time">
                <xsl:value-of select="substring(@StartTime, 12, 8)"/>
            </td>
            <td>
                <xsl:value-of select="@StationName"/>
            </td>	    
            <td>
                <a target="_blank">
                    <xsl:attribute name="href">
                        <xsl:value-of select="@Url" />
                    </xsl:attribute> 
                    <xsl:value-of select="@Title"/>		    
                </a>                
            </td>
        </tr>
    </xsl:template>
</xsl:stylesheet>