/*global angular*/
(function () {
    'use strict';

    angular
        .module('simplAdmin.paymentMollie', [])
        .config(['$stateProvider',
            function ($stateProvider) {
                $stateProvider
                    .state('payments-mollie-config', {
                        url: '/payments/mollie/config',
                        templateUrl: '_content/SimplCommerce.Module.PaymentMollie/admin/mollie/mollie-config-form.html',
                        controller: 'MollieConfigFormCtrl as vm'
                    })
                ;
            }
        ]);
})();
