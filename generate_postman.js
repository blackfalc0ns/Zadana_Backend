const fs = require('fs');
const Converter = require('openapi-to-postmanv2');

const openapiData = fs.readFileSync('swagger.json', {encoding: 'UTF8'});

Converter.convert({ type: 'string', data: openapiData },
  { folderStrategy: 'Tags', includeAuthInfoInExample: false }, (err, conversionResult) => {
    if (!conversionResult.result) {
      console.log('Could not convert', conversionResult.reason);
    }
    else {
      fs.writeFileSync('Zadana_Postman_Collection.json', JSON.stringify(conversionResult.output[0].data, null, 2));
      console.log('Successfully created Zadana_Postman_Collection.json');
    }
  }
);
