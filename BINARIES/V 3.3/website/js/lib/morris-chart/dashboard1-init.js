// Dashboard 1 Morris-chart
$( function () {
	"use strict";


	// Extra chart
	Morris.Area( {
		element: 'extra-area-chart',
		data: [ {
				period: '2001',
				csgo: 40,
				dota2: 40,
				tf2:100
        }, {
				period: '2002',
				trade: 50
        }, {
				period: '2003',
				trade: 60
        }, {
				period: '2004',
				trade: 70
        }, {
				period: '2005',
				trade: 80
        }, {
				period: '2006',
				trade: 90
        }, {
				period: '2007',
				trade: 100
        }


        ],
		lineColors: [ '#26DAD2'],
		xkey: 'period',
		ykeys: [ 'trade' ],
		labels: [ 'trade' ],
		pointSize: 0,
		lineWidth: 0,
		resize: true,
		fillOpacity: 0.8,
		behaveLikeLine: true,
		gridLineColor: '#e0e0e0',
		hideHover: 'auto'

	} );



} );
